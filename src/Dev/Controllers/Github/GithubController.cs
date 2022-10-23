namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using Infrastructure;
using Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Platform.Github;
using Organization = v1.Core.Organization;
using Repository = v1.Platform.Github.Repository;

[EntityRbac(typeof(Github), Verbs = RbacVerb.All)]
public class GithubController : IResourceController<Github>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<GithubController> _logger;

    public GithubController(
        IKubernetesClient kubernetesClient,
        ILogger<GithubController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Github? entity)
    {
        if (entity == null) return null;
        if (entity.Status.CredentialsReference != null) return null;
        if (entity.Metadata.Name != "github") throw new Exception("please call the Github Resource - 'github'");

        var org = await _kubernetesClient.GetOrganization();
        
        var orgNs = org.Metadata.NamespaceProperty;
        var secret = await _kubernetesClient.Get<V1Secret>(entity.Spec.Credentials, orgNs);
        if (secret == null)
        {
            _logger.LogWarning("ensure that you add a github secret");
        }

        //the main org repo
        await _kubernetesClient.Ensure(() => new Repository()
        {
            Metadata = new ()
            {
                Labels = new Dictionary<string, string>
                {
                    { Repository.OwnerLabel(), org.Name() },
                    { Repository.TypeLabel(), "organization" }
                }
            },
            
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Internal,
                OrganizationNamespace = orgNs
            }
        }, orgNs, orgNs);

        var globalTeamName = entity.Spec.GlobalTeam;
        await _kubernetesClient.Ensure(() => new Team
        {
            Spec = new()
            {
                Type = Type.System,
                Visibility = Visibility.Private
            }
        }, globalTeamName, orgNs);

        var archivedTeamName = entity.Spec.ArchiveTeam;
        await _kubernetesClient.Ensure(() => new Team
        {
            Metadata = new ()
            {
                Labels = new Dictionary<string, string>
                {
                    { Repository.OwnerLabel(), org.Name() },
                    { Repository.TypeLabel(), "archive" }
                }
            },
            
            Spec = new()
            {
                Type = Type.System,
                Visibility = Visibility.Private
            }
        }, archivedTeamName, orgNs);

        return null;
    }

    public async Task DeletedAsync(Github? entity)
    {
        if (entity == null)
        {
            return;
        }

        //var orgNs = entity.Metadata.NamespaceProperty;
        //await _kubernetesClient.Delete<Repository>(entity.Spec.GlobalTeam, orgNs);
        //await _kubernetesClient.Delete<Repository>(entity.Spec.ArchiveTeam, orgNs);
    }
}