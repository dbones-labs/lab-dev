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

        var tenancyName = entity.Metadata.Name; //Tenancy.GetNamespaceName(entity.Metadata.Name);
        var organisation = entity.Metadata.NamespaceProperty;
        var teamName = Team.GetTeamName(entity.Metadata.Name);
        var guestTeamName = Team.GetGuestTeamName(entity.Metadata.Name);
        
        var ns = await _kubernetesClient.Get<V1Namespace>(tenancyName);
        if (ns == null) throw new Exception($"requires tenancy namespace {tenancyName}");
        
        /*
         * need to confirm the best place to put these, as this is the control mechanism for a tenancy.
         * need to confirm if the tenancy users will have what level of access to these resouces in this ns
         * 
         * 2 teams
         * 1 repo
         * make the 2 teams as contrib
         *
         * if they are a platform  tenancy, then they git access as write to the org repo.
         */
        
        //TEAMS
        var team = _kubernetesClient.Ensure(() =>new Team
        {
            Metadata = new V1ObjectMeta
            {
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
        }, tenancyName, tenancyName);
        
        var guestTeam = _kubernetesClient.Ensure(() =>new Team
        {
            Spec = new()
            {
                Type = Type.System,
                Visibility = Visibility.Internal
            }
        }, guestTeamName, tenancyName);
        
        //REPO
        var tenancyRepo = await _kubernetesClient.Ensure(() =>new Repository
        {
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Internal,
                OrganizationNamespace = organisation
            }
        }, tenancyName, tenancyName);
        
        
        //COLLB
        var collabName = Collab.GetCollabName(tenancyName, teamName);
        await _kubernetesClient.Ensure(() => Collab.Create(
            tenancyName,
            teamName,
            organisation,
            Membership.Push), collabName, tenancyName);
        
        
        var guestCollabName = Collab.GetCollabName(tenancyName, guestTeamName);
        await _kubernetesClient.Ensure(() => Collab.Create(
            tenancyName,
            guestTeamName,
            organisation,
            Membership.Pull), guestCollabName, tenancyName);

        
        //PLATORM team access to ORG repo
        if (entity.Spec.IsPlatform)
        {
            var platformOrgCollabName = Collab.GetCollabName(organisation, teamName);
            await _kubernetesClient.Ensure(() => Collab.Create(
                organisation,
                teamName,
                organisation,
                Membership.Push), platformOrgCollabName, organisation);
        }
        
        return null;
    }


    public async Task DeletedAsync(Tenancy? entity)
    {
        if (entity == null) return;

        var tenancyName = entity.Metadata.Name;   //Tenancy.GetNamespaceName(entity.Metadata.Name);
        var organisation = entity.Metadata.NamespaceProperty;
        var teamName = Team.GetTeamName(entity.Metadata.Name);
        var guestTeamName = Team.GetGuestTeamName(entity.Metadata.Name);
        
        var collabName = Collab.GetCollabName(tenancyName, teamName);
        var guestCollabName = Collab.GetCollabName(tenancyName, guestTeamName);
        var platformOrgCollabName = Collab.GetCollabName(organisation, teamName);
        
        await _kubernetesClient.Delete<Collaborator>(platformOrgCollabName, organisation);
        await _kubernetesClient.Delete<Collaborator>(collabName, tenancyName);
        await _kubernetesClient.Delete<Collaborator>(guestCollabName, tenancyName);
        
        await _kubernetesClient.Delete<Repository>(tenancyName, tenancyName);
    }
}

