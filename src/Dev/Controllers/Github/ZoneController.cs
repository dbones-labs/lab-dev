namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Github;

[EntityRbac(typeof(Zone), Verbs = RbacVerb.All)]
public class ZoneController : ResourceController<Zone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ZoneController> _logger;

    public ZoneController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<ZoneController> logger
    ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Zone entity)
    {
        var organisation = entity.Metadata.NamespaceProperty;
        var zoneName = entity.Metadata.Name;
        
        var ns = await _kubernetesClient.Get<V1Namespace>(zoneName);
        if (ns == null) throw new Exception($"cannot find zone namespace {zoneName}");

        var github = await _kubernetesClient.GetGithub(organisation);
        var attribute = await _kubernetesClient.Get<ZoneAttribute>(entity.Name(), entity.Namespace());
        if (attribute == null) throw new Exception($"zone {entity.Name()} is missing a attribute");


        await _kubernetesClient.Ensure(() => new Repository()
        {
            Metadata = new ()
            {
                Labels = new Dictionary<string, string>
                {
                    { Repository.OwnerLabel(), zoneName },
                    { Repository.TypeLabel(), "zone" }
                }
            },
            
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Internal,
                OrganizationNamespace = organisation
            }
        }, zoneName, zoneName);

        
        var platformTeams = await _kubernetesClient.List<Team>(
            null,
            new EqualsSelector(Team.PlatformLabel(), "True"));
        
        var collabs = await _kubernetesClient.List<Collaborator>(zoneName);
        var toRemove = collabs
            .Where(x => x.Spec.Team != github.Spec.GlobalTeam)
            .Where(x => x.Spec.Team != github.Spec.ArchiveTeam)
            .ToDictionary(x => x.Spec.Team);

        foreach (var platformTeam in platformTeams)
        {
            var team = platformTeam.Metadata.Name;
            var tenancy = await _kubernetesClient.Get<Tenancy>(platformTeam.Metadata.Name, organisation);
            if (tenancy == null) continue;
            
            //the team do not need access. (if they were collabing they will be removed)
            var accessToZone = tenancy.Spec.Where(attribute);
            if (!accessToZone) continue;
            
            // no change
            if (toRemove.TryGetValue(team, out var existingCollab))
            {
                toRemove.Remove(team);
                continue;
            }
            
            //to add
            var collab = Collaborator.Init(
                zoneName, 
                team, 
                organisation, 
                Membership.Push);

            await _kubernetesClient.Ensure(() => collab, collab.Metadata.Name, zoneName);
        }

        //delete all old collabs
        foreach (var removeCollab in toRemove.Values)
        {
            await _kubernetesClient.Delete(removeCollab);
        }

        return null;
    }


    protected override async Task InternalDeletedAsync(Zone entity)
    {
        var zoneName = entity.Name();
        
        var collabs = await _kubernetesClient.List<Collaborator>(zoneName);
        await _kubernetesClient.Delete(collabs);

        await _kubernetesClient.Delete<Repository>(zoneName, zoneName);
    }
}