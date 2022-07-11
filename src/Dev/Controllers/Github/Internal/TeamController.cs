﻿namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;
using Team = v1.Platform.Github.Team;

[EntityRbac(typeof(Team), Verbs = RbacVerb.All)]
public class TeamController :  IResourceController<Team>
{
    private readonly GitHubClient _gitHubClient;
    private readonly KubernetesClient _kubernetesClient;
    private readonly ILogger<TeamController> _logger;

    public TeamController(
        GitHubClient gitHubClient,
        KubernetesClient kubernetesClient,
        ILogger<TeamController> logger)
    {
        _gitHubClient = gitHubClient;
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(Team? entity)
    {
        if (entity == null) return null;
        
        var github = await _kubernetesClient.Get<Github>("github", entity.Metadata.NamespaceProperty);
        if (github == null) throw new Exception("cannot find 'github' resource");
        
        _logger.LogInformation("reconciling team: {name}", entity.Metadata.Name);
        
        var meta = entity.Metadata;
        var spec = entity.Spec;
        var status = entity.Status;
        var org = github.Spec.Organisation;
        
        Octokit.Team? team = null;
        
        if (status.Id.HasValue) team = await _gitHubClient.Organization.Team.Get(status.Id.Value);
        if (team == null)
        {
            var teams = await _gitHubClient.Organization.Team.GetAll(org);
            team = teams.FirstOrDefault(x => x.Name == meta.Name);
        }

        if (team == null)
        {
            team = await _gitHubClient.Organization.Team.Create(org, new NewTeam(meta.Name)
            {
                Description = spec.Description,
                Privacy = spec.Visibility == Visibility.Private ? TeamPrivacy.Secret : TeamPrivacy.Closed
            });

            status.Id = team.Id;
        }
        
        else
        {
            await _gitHubClient.Organization.Team.Update(team.Id, new UpdateTeam(meta.Name)
            {
                Description = spec.Description,
                Privacy  = spec.Visibility == Visibility.Private ? TeamPrivacy.Secret : TeamPrivacy.Closed
            });
        }

        return null;
    }


    public async Task DeletedAsync(Team? entity)
    {
        if (entity == null) return;

        var github = await _kubernetesClient.Get<Github>("github");
        if (github == null) throw new Exception("cannot find 'github' resource");
        
        if (!entity.Status.Id.HasValue) return;
        await _gitHubClient.Organization.Team.Delete(entity.Status.Id.Value);
    }
}