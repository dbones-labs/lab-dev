﻿namespace Dev.Controllers.Github.Internal;

using Dev.v1.Platform.Github;
using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using Team = v1.Platform.Github.Team;

[EntityRbac(typeof(Team), Verbs = RbacVerb.All)]
public class TeamController  :  ResourceController<Team>
{
    private readonly GitHubClient _gitHubClient;
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TeamController> _logger;

    public TeamController(
        ResourceCache resourceCache,
        GitHubClient gitHubClient,
        IKubernetesClient kubernetesClient,
        ILogger<TeamController> logger
        ) : base(resourceCache)
    {
        _gitHubClient = gitHubClient;
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Team entity)
    {
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if (!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);

        _logger.LogInformation("reconciling team: {name}", entity.Metadata.Name);

        var meta = entity.Metadata;
        var spec = entity.Spec;
        var status = entity.Status;
        var org = github.Spec.Organisation;

        Octokit.Team? team = null;

        //get the team by id (incase name is chanaged)
        if (status.Id.HasValue) team = await HttpAssist.Get(() => _gitHubClient.Organization.Team.Get(status.Id.Value));
        if (team == null)
        {
            //find by name (if the id was not set)
            var teams = await _gitHubClient.Organization.Team.GetAll(org);
            team = teams.FirstOrDefault(x => x.Name == meta.Name);
        }

        //ok we are creating
        if (team == null)
        {
            team = await _gitHubClient.Organization.Team.Create(org, new NewTeam(meta.Name)
            {
                Description = spec.Description,
                Privacy = spec.Visibility == Visibility.Private ? TeamPrivacy.Secret : TeamPrivacy.Closed
            });
        }

        else
        {
            //update, we need to figure out what to update tho
            string? updateDescription = null;
            TeamPrivacy? updateTeamPrivacy = null;
            bool shouldUpdate = false;

            var expectedDescription = entity.Spec.Description;
            var shouldUpdateDescription = team.Description != expectedDescription;
            if (shouldUpdateDescription)
            {
                updateDescription = expectedDescription;
                shouldUpdate = true;
            }

            var expectedTeamPrivacy = spec.Visibility == Visibility.Private ? TeamPrivacy.Secret : TeamPrivacy.Closed;
            var shouldUpdateTeamPrivacy = team.Privacy != expectedTeamPrivacy;
            if (shouldUpdateTeamPrivacy)
            {
                updateTeamPrivacy = expectedTeamPrivacy;
                shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                team = await _gitHubClient.Organization.Team.Create(org, new NewTeam(meta.Name)
                {
                    Description = updateDescription,
                    Privacy = updateTeamPrivacy
                });
            }
        }

        //we need to store the id (this is how we know that we have created the team on Github)
        if (!status.Id.HasValue)
        {
            status.Id = team.Id;
            await _kubernetesClient.UpdateStatus(entity);
        }

        return null;
}


    protected override async Task InternalDeletedAsync(Team entity)
    {
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        if (!entity.Status.Id.HasValue) return;
        await _gitHubClient.Organization.Team.Delete(entity.Status.Id.Value);
    }
}