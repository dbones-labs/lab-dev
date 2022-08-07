﻿namespace Dev.Controllers.Github.Internal;

using Dev.v1.Platform.Github;
using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Core.Tenancies;
using Repository = v1.Platform.Github.Repository;
using State = v1.Platform.Github.State;

/// <summary>
/// manage the repo only, for team access this is via collaborators
/// </summary>
/// <remarks>
/// maintains the global teams access
/// </remarks>
[EntityRbac(typeof(Repository), Verbs = RbacVerb.All)]
public class RepositoryController : IResourceController<Repository>
{
    private readonly GitHubClient _gitHubClient;
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(
        GitHubClient gitHubClient,
        IKubernetesClient kubernetesClient,
        ILogger<RepositoryController> logger)
    {
        _gitHubClient = gitHubClient;
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(Repository? entity)
    {
        if (entity == null) return null;
        
        var github = await _kubernetesClient.GetGithub(entity.Spec.OrganizationNamespace);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        _logger.LogInformation("reconciling repository: {name}", entity.Metadata.Name);
        var meta = entity.Metadata;
        var spec = entity.Spec;
        var org = entity.Spec.OrganizationNamespace;

        //ensure the repo exists and is update
        var repository = await HttpAssist.Get(()=> _gitHubClient.Repository.Get(github.Spec.Organisation, meta.Name));
        
        if (repository == null)
        {
            var newRepository = new NewRepository(meta.Name)
            {
                AutoInit = true,
                HasIssues = true,
                HasDownloads = true,
                //TeamId = spec.OwnerId,
                Private = spec.Visibility != Visibility.Public,
                Visibility = spec.Visibility == Visibility.Public
                    ? RepositoryVisibility.Public
                    : RepositoryVisibility.Private,
            };
            repository = await _gitHubClient.Repository.Create(github.Spec.Organisation, newRepository);
        }
        else
        {
            //it seems some of the values if applied to the same value is not allows
            RepositoryUpdate? repoUpdate = null;
            bool? isPrivate = null;
            bool? isArchived = null;
            RepositoryVisibility? setVisibility = null;
            bool shouldUpdate = false;
            
            var expectedPrivate = spec.Visibility != Visibility.Public;
            var shouldUpdatePrivate = repository.Private != expectedPrivate;
            if (shouldUpdatePrivate)
            {
                isPrivate = expectedPrivate;
                shouldUpdate = true;
            }

            var expectedArchived = spec.State == State.Archived;
            var shouldUpdateArchived = repository.Archived != expectedArchived;
            if (shouldUpdateArchived)
            {
                isArchived = expectedArchived;
                shouldUpdate = true;
            }
            
            var expectedVisibility = spec.Visibility == Visibility.Public
                ? RepositoryVisibility.Public
                : RepositoryVisibility.Private;
            var shouldUpdateVisibility = repository.Visibility != expectedVisibility;
            if (shouldUpdateVisibility)
            {
                setVisibility = expectedVisibility;
                shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                repoUpdate = new RepositoryUpdate(repository.Name)
                {
                    Private = isPrivate,
                    Archived = isArchived,
                    Visibility = setVisibility

                };
                repository = await _gitHubClient.Repository.Edit(repository.Id, repoUpdate);
                
            }
        }

        if (entity.Status.Id == null)
        {
            entity.Status.Id = repository.Id;
        }

        //this is a tenancy which we can raise FF access to
        if (entity.Spec.Type == Type.System)
        {
            await EnsureLabel(repository, FireFighter.Activated());
            await EnsureLabel(repository, FireFighter.Approved());
            await EnsureLabel(repository, FireFighter.Requested());
        }
        
        var globalCollabName = Collaborator.GetCollabName(meta.Name, github.Spec.GlobalTeam);
        if (spec.Visibility == Visibility.Private)
        {
            //ensure that only the repo teams can access, not the org team
            await _kubernetesClient.Delete<Collaborator>(globalCollabName, meta.NamespaceProperty);
        }
        else
        {
            //allow the global team read access
            var globalCollab =
                await _kubernetesClient.Get<Collaborator>(globalCollabName, meta.NamespaceProperty);
            if (globalCollab == null)
            {
                globalCollab = Collaborator.Init(
                    meta.Name, 
                    github.Spec.GlobalTeam, 
                    org, 
                    Membership.Pull);
                
                await _kubernetesClient.Create(()=> globalCollab, globalCollab.Metadata.Name, meta.NamespaceProperty);
            }
        }

        
        var archiveCollabName = Collab.GetCollabName(meta.Name, github.Spec.ArchiveTeam);
        if (spec.State == State.Archived)
        {
            var archiveCollab =
                await _kubernetesClient.Get<Collaborator>(archiveCollabName, meta.NamespaceProperty);
            if (archiveCollab == null)
            {
                archiveCollab = Collaborator.Init(meta.Name, github.Spec.ArchiveTeam, org, Membership.Admin);
                archiveCollab.Metadata.NamespaceProperty = meta.NamespaceProperty;
                
                await _kubernetesClient.Create(archiveCollab);
            }
        }
        else
        {
            await _kubernetesClient.Delete<Collaborator>(archiveCollabName, meta.NamespaceProperty);
        }

        return null;
    }

    private async Task EnsureLabel(Octokit.Repository repository, string name)
    {
        var requestedLabel = await HttpAssist.Get(()=> _gitHubClient.Issue.Labels.Get(repository.Id, name));
        if (requestedLabel == null)
        {
            await _gitHubClient.Issue.Labels.Create(repository.Id, new NewLabel(name, "1C2AA7"));
        }
    }

    public async Task DeletedAsync(Repository? entity)
    {
        if (entity == null) return;

        var github = await _kubernetesClient.GetGithub(entity.Spec.OrganizationNamespace);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);

        if (!entity.Status.Id.HasValue) return;
        var repositoryId = entity.Status.Id.Value;

        var meta = entity.Metadata;

        //if we do not need to archive then we just DELETE!
        if (!github.Spec.Archive)
        {
            await _gitHubClient.Repository.Delete(repositoryId);
            return;
        }

        //remove global team access
        var globalCollabName = Collaborator.GetCollabName(meta.Name, github.Spec.GlobalTeam);
        await _kubernetesClient.Delete<Collaborator>(globalCollabName, entity.Metadata.NamespaceProperty);

        //confirm archiving
        var archiveCollabName = Collaborator.GetCollabName(meta.Name, github.Spec.ArchiveTeam);
        var archiveCollab =
            await _kubernetesClient.Get<Collaborator>(archiveCollabName, entity.Metadata.NamespaceProperty);

        if (archiveCollab == null)
        {
            archiveCollab = Collaborator.Init(meta.Name,
                github.Spec.ArchiveTeam,
                entity.Spec.OrganizationNamespace,
                Membership.Admin);

            archiveCollab.Metadata.NamespaceProperty = meta.NamespaceProperty;

            await _kubernetesClient.Create(archiveCollab);
        }
    }
}