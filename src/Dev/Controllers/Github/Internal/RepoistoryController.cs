namespace Dev.Controllers.Github.Internal;

using Dev.v1.Platform.Github;
using DotnetKubernetesClient;
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
    private readonly KubernetesClient _kubernetesClient;
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(
        GitHubClient gitHubClient,
        KubernetesClient kubernetesClient,
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
        var repository = await _gitHubClient.Repository.Get(meta.NamespaceProperty, meta.Name);
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
            repository = await _gitHubClient.Repository.Create(newRepository);
        }
        else
        {
            repository = await _gitHubClient.Repository.Edit(repository.Id, new RepositoryUpdate(repository.Name)
            {
                Archived = spec.State == State.Archived,
                Private = spec.Visibility != Visibility.Public,
                Visibility = spec.Visibility == Visibility.Public
                    ? RepositoryVisibility.Public
                    : RepositoryVisibility.Private
            });
        }

        if (entity.Status.Id == null)
        {
            entity.Status.Id = repository.Id;
        }
        
        await EnsureLabel(repository, FireFighter.Activated());
        await EnsureLabel(repository, FireFighter.Approved());
        await EnsureLabel(repository, FireFighter.Requested());

        var globalCollabName = Collab.GetCollabName(meta.Name, github.Spec.GlobalTeam);
        if (spec.Visibility == Visibility.Private)
        {
            await _kubernetesClient.Delete<Collaborator>(globalCollabName, org);
        }
        else
        {
            var globalCollab =
                await _kubernetesClient.Get<Collaborator>(globalCollabName, org);
            if (globalCollab == null)
            {
                globalCollab = Collab.Create(
                    meta.Name,
                    github.Spec.GlobalTeam,
                    org,
                    Membership.Pull);

                await _kubernetesClient.Create(globalCollab);
            }
        }

        
        var archiveCollabName = Collab.GetCollabName(meta.Name, github.Spec.ArchiveTeam);
        if (spec.State == State.Archived)
        {
            var archiveCollab =
                await _kubernetesClient.Get<Collaborator>(archiveCollabName, org);
            if (archiveCollab == null)
            {
                archiveCollab = Collab.Create(
                    meta.Name,
                    github.Spec.ArchiveTeam,
                    org,
                    Membership.Admin);
                
                await _kubernetesClient.Create(archiveCollab);
            }
        }
        else
        {
            await _kubernetesClient.Delete<Collaborator>(archiveCollabName, org);
        }

        return null;
    }

    private async Task EnsureLabel(Octokit.Repository repository, string name)
    {
        var requestedLabel = await _gitHubClient.Issue.Labels.Get(repository.Id, name);
        if (requestedLabel == null)
        {
            await _gitHubClient.Issue.Labels.Create(repository.Id, new NewLabel(name, "#1C2AA7"));
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
        var globalCollabName = Collab.GetCollabName(meta.Name, github.Spec.GlobalTeam);
        await _kubernetesClient.Delete<Collaborator>(globalCollabName, entity.Spec.OrganizationNamespace);

        //confirm archiving
        var archiveCollabName = Collab.GetCollabName(meta.Name, github.Spec.ArchiveTeam);
        var archiveCollab =
            await _kubernetesClient.Get<Collaborator>(archiveCollabName, entity.Spec.OrganizationNamespace);

        if (archiveCollab == null)
        {
            archiveCollab = Collab.Create(
                meta.Name,
                github.Spec.ArchiveTeam,
                entity.Spec.OrganizationNamespace,
                Membership.Admin);

            await _kubernetesClient.Create(archiveCollab);
        }
    }
}