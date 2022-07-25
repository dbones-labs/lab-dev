namespace Dev.Controllers.Github.Internal;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;
using Team = v1.Platform.Github.Team;

[EntityRbac(typeof(Collaborator), Verbs = RbacVerb.All)]
public class CollaboratorController  : IResourceController<Collaborator>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<CollaboratorController> _logger;

    public CollaboratorController(
        IKubernetesClient kubernetesClient,
        GitHubClient gitHubClient,
        ILogger<CollaboratorController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Collaborator? entity)
    {
        if (entity == null) return null;

        var github = await _kubernetesClient.GetGithub(entity.Spec.OrganizationNamespace);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);

        _logger.LogInformation("reconciling collaborator: {name}", entity.Metadata.Name);

        var spec = entity.Spec;
        var org = github.Spec.Organisation;

        var team = await _kubernetesClient.Get<Team>(spec.Team, github.Metadata.NamespaceProperty);
        if (team == null) throw new Exception($"cannot find team {spec.Team}");
        if (!team.Status.Id.HasValue) throw new Exception($"missing id for team {spec.Team}");

        var permission = spec.Membership switch
        {
            Membership.Pull => Permission.Pull,
            Membership.Push => Permission.Push,
            Membership.Admin => Permission.Admin,
            _ => throw new ArgumentOutOfRangeException()
        };

        await _gitHubClient.Organization.Team.AddRepository(
            team.Status.Id.Value,
            org,
            spec.Repository,
            new RepositoryPermissionRequest(permission));

        return null;
    }

    public async Task DeletedAsync(Collaborator? entity)
    {
        if (entity == null) return;

        var github = await _kubernetesClient.GetGithub(entity.Spec.OrganizationNamespace);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        if(!string.IsNullOrWhiteSpace(token)) _gitHubClient.Auth(token);
        
        var spec = entity.Spec;
        var org = github.Spec.Organisation;

        var team = await _kubernetesClient.Get<Team>(spec.Team, org);
        if (team == null) return;
        if (!team.Status.Id.HasValue) throw new Exception("team does not have an id");
        
        await _gitHubClient.Organization.Team.RemoveRepository(
            team.Status.Id.Value,
            org,
            spec.Repository);
    }
}