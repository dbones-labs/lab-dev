namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Github;

[EntityRbac(typeof(Zone), Verbs = RbacVerb.All)]
public class ZoneController : IResourceController<Zone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ZoneController> _logger;

    public ZoneController(
        IKubernetesClient kubernetesClient,
        ILogger<ZoneController> logger
    )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Zone? entity)
    {
        if (entity == null) return null;

        var organisation = entity.Metadata.NamespaceProperty;
        var zoneName = entity.Metadata.Name;
        
        var ns = await _kubernetesClient.Get<V1Namespace>(zoneName);
        if (ns == null) throw new Exception($"cannot find zone namespace {zoneName}");

        var github = await _kubernetesClient.GetGithub(organisation);
        
        try
        {
            await _kubernetesClient.Ensure(() => new Repository()
            {
                Spec = new()
                {
                    EnforceCollaborators = false,
                    State = State.Active,
                    Type = Type.System,
                    Visibility = Visibility.Internal,
                    OrganizationNamespace = organisation
                }
            }, zoneName, zoneName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
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
            var accessToZone = tenancy.Spec.Where(entity);
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


    public async Task DeletedAsync(Zone? entity)
    {
        if (entity == null) return;
        var zoneName = entity.Metadata.Name;
        
        var collabs = await _kubernetesClient.List<Collaborator>(zoneName);
        await _kubernetesClient.Delete(collabs);

        await _kubernetesClient.Delete<Repository>(zoneName, zoneName);
    }
}