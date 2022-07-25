namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Platform.Github;

[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : IResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        IKubernetesClient kubernetesClient,
        ILogger<TenancyController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Tenancy? entity)
    {
        if (entity == null) return null;

        var @namespace = entity.Metadata.Name; //Tenancy.GetNamespaceName(entity.Metadata.Name);
        var organisation = entity.Metadata.NamespaceProperty;
        var teamName = Team.GetTeamName(entity.Metadata.Name);
        var guestTeamName = Team.GetGuestTeamName(entity.Metadata.Name);
        
        var team = await _kubernetesClient.Get<Team>(teamName, organisation);
        if (team == null)
        {
            team = new()
            {
                Metadata = new()
                {
                    Name = teamName,
                    NamespaceProperty = organisation,
                    Labels = new Dictionary<string, string>()
                    {
                        { Team.PlatformLabel(), entity.Spec.IsPlatform ? "True" : "False" }
                    }
                },
                
                Spec = new()
                {
                    Type = Type.Normal,
                    Visibility = Visibility.Internal
                }
            };

            await _kubernetesClient.Create(team);
        }
        
        var guestTeam = await _kubernetesClient.Get<Team>(guestTeamName, organisation);
        if (guestTeam == null)
        {
            guestTeam = new()
            {
                Metadata = new()
                {
                    Name = guestTeamName,
                    NamespaceProperty = organisation
                },
                
                Spec = new()
                {
                    Type = Type.System,
                    Visibility = Visibility.Internal
                }
            };

            await _kubernetesClient.Create(guestTeam);
        }
        
        var ns = await _kubernetesClient.Get<V1Namespace>(@namespace);
        if (ns == null) throw new Exception($"requires tenancy namespace {@namespace}");

        var tenancyRepo = await _kubernetesClient.Get<Repository>(@namespace, @namespace);
        if (tenancyRepo == null)
        {
            tenancyRepo = new()
            {
                Metadata = new()
                {
                    Name = @namespace,
                    NamespaceProperty = @namespace
                },
                Spec = new()
                {
                    EnforceCollaborators = false,
                    State = State.Active,
                    Type = Type.System,
                    Visibility = Visibility.Internal,
                    OrganizationNamespace = organisation
                }
            };

            await _kubernetesClient.Create(tenancyRepo);
        }

        var collabName = Collab.GetCollabName(tenancyRepo.Metadata.Name, teamName);
        var collab = await _kubernetesClient.Get<Collaborator>(collabName, organisation);
        if (collab == null)
        {
            collab = Collab.Create(
                tenancyRepo.Metadata.Name,
                teamName,
                organisation,
                Membership.Push);
            
            await _kubernetesClient.Create(collab);
        }
        
        var guestCollabName = Collab.GetCollabName(tenancyRepo.Metadata.Name, guestTeamName);
        var guestCollab = await _kubernetesClient.Get<Collaborator>(guestCollabName, organisation);
        if (guestCollab == null)
        {
            guestCollab = Collab.Create(
                tenancyRepo.Metadata.Name,
                guestTeamName,
                organisation,
                Membership.Pull);
            
            await _kubernetesClient.Create(guestCollab);
        }

        if (entity.Spec.IsPlatform)
        {
            var platformOrgCollabName = Collab.GetCollabName(organisation, teamName);
            var platformOrgCollab = await _kubernetesClient.Get<Collaborator>(platformOrgCollabName, organisation);
            if (platformOrgCollab == null)
            {
                platformOrgCollab = Collab.Create(
                    organisation,
                    teamName,
                    organisation,
                    Membership.Push);
            
                await _kubernetesClient.Create(platformOrgCollab);
            }
            
            //todo add the Zones
        }
        
        return null;
    }


    public async Task DeletedAsync(Tenancy? entity)
    {
        if (entity == null) return;

        var @namespace = entity.Metadata.Name;   //Tenancy.GetNamespaceName(entity.Metadata.Name);
        var organisation = entity.Metadata.NamespaceProperty;
        var teamName = Team.GetTeamName(entity.Metadata.Name);
        var guestTeamName = Team.GetGuestTeamName(entity.Metadata.Name);
        
        var collabName = Collab.GetCollabName(@namespace, teamName);
        var guestCollabName = Collab.GetCollabName(@namespace, guestTeamName);
        var platformOrgCollabName = Collab.GetCollabName(organisation, teamName);
        
        await _kubernetesClient.Delete<Collaborator>(collabName, organisation);
        await _kubernetesClient.Delete<Collaborator>(guestCollabName, organisation);
        await _kubernetesClient.Delete<Collaborator>(platformOrgCollabName, organisation);
        
        await _kubernetesClient.Delete<Repository>(@namespace, @namespace);
    }
}

