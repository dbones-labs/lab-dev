namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Tenancies;
using v1.Platform.Github;
using State = v1.Platform.Github.State;

[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : ResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<TenancyController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Tenancy entity)
    {
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
                    { Team.PlatformLabel(), entity.Spec.IsPlatform ? "True" : "False" },
                    { Team.TenancyLabel(), tenancyName },
                    { Team.GuestLabel(), "False" }
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
            Metadata = new V1ObjectMeta
            {
                Labels = new Dictionary<string, string>()
                {
                    { Team.TenancyLabel(), tenancyName },
                    { Team.GuestLabel(), "True" }
                }
            },
            Spec = new()
            {
                Type = Type.System,
                Visibility = Visibility.Internal
            }
        }, guestTeamName, tenancyName);
        
        //REPO
        var tenancyRepo = await _kubernetesClient.Get<Repository>(tenancyName, tenancyName);
        if (tenancyRepo == null)
        {
            await _kubernetesClient.Create(() =>new Repository
            {
                Metadata = new ()
                {
                    Labels = new Dictionary<string, string>
                    {
                        { Repository.OwnerLabel(), entity.Name() },
                        { Repository.TypeLabel(), "tenancy" }
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
            }, tenancyName, tenancyName);
        }
        else
        {
            var spec = tenancyRepo.Spec;
            
            //restoring a team from archive (just re-apply the correct settings)
            spec.EnforceCollaborators = false;
            spec.State = State.Active;
            spec.Visibility = Visibility.Internal;
            spec. OrganizationNamespace = organisation;
            
            await _kubernetesClient.Update(tenancyRepo);
        }

        //COLLB
        var collab = Collaborator.Init(tenancyName, teamName, organisation, Membership.Push);
        await _kubernetesClient.Ensure(() => collab, collab.Metadata.Name, tenancyName);
        
        var guestCollab = Collaborator.Init(tenancyName, guestTeamName, organisation, Membership.Pull);
        await _kubernetesClient.Ensure(() => guestCollab, guestCollab.Metadata.Name, tenancyName);

        
        //house keeping
        var github = await _kubernetesClient.GetGithub(organisation);
        
        //Handle Provisioning
        var members = await _kubernetesClient.List<Member>(tenancyName);
        var globalCollab = Collaborator.Init(tenancyName, github.Spec.GlobalTeam, organisation, Membership.Push);
        if (members.All(x => x.Spec.Role != MemberRole.Owner))
        {
            //no owner
            //if we have no team members (no owners!), we will add the org as an owner, until we get members (someone needs to own it)
            await _kubernetesClient.Ensure(() => globalCollab, globalCollab.Metadata.Name, tenancyName);
        }
        else
        {
            //we have an owner.
            await _kubernetesClient.Delete(globalCollab);
        }
        
        
        //PLATORM team access to ORG repo
        if (entity.Spec.IsPlatform)
        {
            var platformOrgCollab = Collaborator.Init(github.Spec.GlobalTeam, teamName, organisation, Membership.Push);
            await _kubernetesClient.Ensure(() => platformOrgCollab, platformOrgCollab.Metadata.Name, organisation);
        }

        return null;
    }


    protected override async Task InternalDeletedAsync(Tenancy entity)
    {
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

        await _kubernetesClient.Delete<Team>(tenancyName, tenancyName);
        
        //archive tenancy repo (as issues hold an audit of firefigter)
        var tenancyRepo = await _kubernetesClient.Get<Repository>(tenancyName, tenancyName);
        if (tenancyRepo != null)
        {
            var spec = tenancyRepo.Spec;
            spec.State = State.Archived;
            await _kubernetesClient.Update(tenancyRepo);
        }
    }
}

