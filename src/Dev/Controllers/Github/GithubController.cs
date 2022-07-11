namespace Dev.Controllers.Github;

using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Octokit;
using v1.Platform.Github;
using Organization = v1.Core.Organization;
using Repository = v1.Platform.Github.Repository;

[EntityRbac(typeof(Github), Verbs = RbacVerb.All)]
public class GithubController : IResourceController<Github>
{
    private readonly KubernetesClient _kubernetesClient;
    private readonly GitHubClient _gitHubClient;
    private readonly ILogger<GithubController> _logger;

    public GithubController(
        KubernetesClient kubernetesClient,
        GitHubClient gitHubClient,
        ILogger<GithubController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitHubClient = gitHubClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(Github? entity)
    {
        if (entity == null) return null;
        if (entity.Status.CredentialsReference != null) return null;
        if (entity.Metadata.Name != "github") throw new Exception("please call the Github Resource - 'github'");
        
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
            
        var orgNs = org.Metadata.NamespaceProperty;
        var secret = await _kubernetesClient.Get<V1Secret>(entity.Spec.Credentials, orgNs);
        if (secret == null)
        {
            _logger.LogWarning("ensure that you add a github secret");
        }

        var globalTeamName = entity.Spec.GlobalTeam;
        var globalTeam = await _kubernetesClient.Get<Repository>(globalTeamName, orgNs);
        if (globalTeam == null)
        {
            await _kubernetesClient.Create(new Repository
            {
                Metadata = new()
                {
                    Name = globalTeamName,
                    NamespaceProperty = orgNs
                }, 
                
                Spec = new()
                {
                    State = State.Active,
                    Type = Type.System,
                    Visibility = Visibility.Private,
                    //Owner = "generated"
                }
            });
        }
        
        var archivedTeamName = entity.Spec.ArchiveTeam;
        var archivedTeam = await _kubernetesClient.Get<Repository>(archivedTeamName, orgNs);
        if (archivedTeam == null)
        {
            await _kubernetesClient.Create(new Repository
            {
                Metadata = new()
                {
                    Name = archivedTeamName,
                    NamespaceProperty = orgNs
                },

                Spec = new()
                {
                    State = State.Active,
                    Type = Type.System,
                    Visibility = Visibility.Private,
                    //Owner = "generated"
                }
            });
        }

        return null;
    }
    
    public async Task DeletedAsync(Github? entity)
    {
        if (entity == null)
        {
            return;
        }

        var orgNs = entity.Metadata.NamespaceProperty;
        await _kubernetesClient.Delete<Repository>(entity.Spec.GlobalTeam, orgNs);
        await _kubernetesClient.Delete<Repository>(entity.Spec.ArchiveTeam, orgNs);
    }
}