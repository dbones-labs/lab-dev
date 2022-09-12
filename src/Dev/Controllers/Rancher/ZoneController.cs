namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Rancher.External.Fleet;

/// <summary>
/// sets up the zone area, which will be acted upon by zone level components (kubernetes, postgres, rabbit etc)
/// </summary>
[EntityRbac(typeof(Zone), Verbs = RbacVerb.All)]
public class ZoneController : IResourceController<Zone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ZoneController> _logger;

    public ZoneController(
        IKubernetesClient kubernetesClient,
        ILogger<ZoneController> logger
    )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Zone? entity)
    {
        if (entity == null) return null;

        var environments = await _kubernetesClient.List<Environment>(entity.Metadata.NamespaceProperty);
        var env = environments.FirstOrDefault(x => x.Metadata.Name == entity.Spec.Environment);
        if (env == null) throw new Exception($"cannot find environment {entity.Spec.Environment}");

        if (entity.Status.Type != env.Spec.Type)
        {
            entity.Status.Type = env.Spec.Type;
            await _kubernetesClient.UpdateStatus(entity);
        }
        
        await _kubernetesClient.Ensure(() => new V1Namespace(), entity.Metadata.Name);
        
        await _kubernetesClient.Ensure(() => new TenancyContext
        {
            Spec = new TenancyContextSpec()
            {
                OrganizationNamespace = entity.Metadata.NamespaceProperty
            }
        }, TenancyContext.GetName(), entity.Metadata.Name);

        if (entity.Name() == "control")
        {
            return null;
        }

        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        
        
        //sync the repo with control and any downstream cluster
        var local = "fleet-local";
        var @default = "fleet-default";
        var githubToken = "github-token";
        var repo = $"https://github.com/{github.Spec.Organisation}/{entity.Name()}.git";
        
        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = repo,
                ClientSecretName = githubToken,
                Paths = new List<string>() {"clusters"},
                TargetNamespace = entity.Name(),
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "control"
                    }
                }
            }
        }, entity.Name(), local);
        
        await _kubernetesClient.Ensure(() => new GitRepo
        {
            Spec = new GitRepoSpec()
            {
                Branch = "main",
                Repo = repo,
                ClientSecretName = githubToken,
                Paths = new List<string>() { "cd" },
                //TargetNamespace = entity.Name(),
                Targets = new List<ClusterSelector>()
                {
                    new ClusterSelector()
                    {
                        ClusterGroup = "delivery"
                    }
                }
            }
        }, entity.Name(), @default);
        
        return null;
    }

    public async Task DeletedAsync(Zone? entity)
    {
        if (entity == null) return;

        var local = "fleet-local";
        var @default = "fleet-default";       
        
        await _kubernetesClient.Delete<V1Namespace>(entity.Metadata.Name, entity.Metadata.NamespaceProperty);
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), local);
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), @default);
    }
}