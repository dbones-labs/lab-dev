namespace Dev.Controllers.Github.Internal;

using System.Runtime.CompilerServices;
using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;

[EntityRbac(typeof(TeamMember), Verbs = RbacVerb.All)]
public class TeamMemberController : ResourceController<TeamMember>
{
    private readonly GitHubClient _gitHubClient;
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TeamMemberController> _logger;

    public TeamMemberController(
        ResourceCache resourceCache,
        GitHubClient gitHubClient,
        IKubernetesClient kubernetesClient,
        ILogger<TeamMemberController> logger
        ) : base(resourceCache)
    {
        _gitHubClient = gitHubClient;
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(TeamMember entity)
    {
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        var spec = entity.Spec;
        var org = github.Spec.Organisation;
        
        var team = await HttpAssist.Get(() => _kubernetesClient.Get<Dev.v1.Platform.Github.Team>(spec.Team, entity.Metadata.NamespaceProperty));
        if (team == null) throw new Exception($"cannot find team {spec.Team}");
        if (!team.Status.Id.HasValue) throw new Exception($"missing id for team {spec.Team}");

        var membershipDetails = await HttpAssist.Get(() => _gitHubClient.Organization.Team.GetMembershipDetails(team.Status.Id.Value, spec.Login));
        if (membershipDetails == null)
        {
            await _gitHubClient.Organization.Team.AddOrEditMembership(
                team.Status.Id.Value, 
                spec.Login, 
                new UpdateTeamMembership(TeamRole.Member));
        }

        return null;
    }
    
    protected override async Task InternalDeletedAsync(TeamMember entity)
    {
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        var spec = entity.Spec;
        var org = github.Spec.Organisation;
        
        var team = await _kubernetesClient.Get<Dev.v1.Platform.Github.Team>(spec.Team, entity.Namespace());
        if (team == null) return;
        if (!team.Status.Id.HasValue) throw new Exception($"missing id for team {spec.Team}");

        await _gitHubClient.Organization.Team.RemoveMembership(team.Status.Id.Value, spec.Login);
    }
    
}