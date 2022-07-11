﻿namespace Dev.Controllers.Github.Internal;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;

[EntityRbac(typeof(TeamMember), Verbs = RbacVerb.All)]
public class TeamMemberController :  IResourceController<TeamMember>
{
    private readonly GitHubClient _gitHubClient;
    private readonly KubernetesClient _kubernetesClient;
    private readonly ILogger<TeamMemberController> _logger;

    public TeamMemberController(
        GitHubClient gitHubClient,
        KubernetesClient kubernetesClient,
        ILogger<TeamMemberController> logger)
    {
        _gitHubClient = gitHubClient;
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(TeamMember? entity)
    {
        if (entity == null) return null;

        var github = await _kubernetesClient.Get<Github>("github");
        if (github == null) throw new Exception("cannot find 'github' resource");
        
        var spec = entity.Spec;
        var org = github.Spec.Organisation;
        
        var team = await _kubernetesClient.Get<Dev.v1.Platform.Github.Team>(spec.Team, org);
        if (team == null) throw new Exception($"cannot find team {spec.Team}");
        if (!team.Status.Id.HasValue) throw new Exception($"missing id for team {spec.Team}");

        await _gitHubClient.Organization.Team.AddOrEditMembership(
            team.Status.Id.Value, 
            spec.Login, 
            new UpdateTeamMembership(TeamRole.Member));

        return null;
    }
    
    public async Task DeletedAsync(TeamMember? entity)
    {
        if (entity == null) return;

        var github = await _kubernetesClient.Get<Github>("github");
        if (github == null) throw new Exception("cannot find 'github' resource");
        
        var spec = entity.Spec;
        var org = github.Spec.Organisation;
        
        var team = await _kubernetesClient.Get<Dev.v1.Platform.Github.Team>(spec.Team, org);
        if (team == null) return;
        if (!team.Status.Id.HasValue) throw new Exception($"missing id for team {spec.Team}");

        await _gitHubClient.Organization.Team.RemoveMembership(team.Status.Id.Value, spec.Login);
    }
    
}