namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Rancher.External.Fleet;

/// <summary>
/// sets up the zone area, which will be acted upon by zone level components (kubernetes, postgres, rabbit etc)
/// </summary>
[EntityRbac(typeof(Zone), Verbs = RbacVerb.All)]
public class ZoneController : ResourceController<Zone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ZoneController> _logger;

    public ZoneController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<ZoneController> logger
    ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Zone entity)
    {
        var environments = await _kubernetesClient.List<Environment>(entity.Metadata.NamespaceProperty);
        var env = environments.FirstOrDefault(x => x.Metadata.Name == entity.Spec.Environment);
        if (env == null) throw new Exception($"cannot find environment {entity.Spec.Environment}");

        if (entity.Status.Type != env.Spec.Type)
        {
            entity.Status.Type = env.Spec.Type;
            await _kubernetesClient.UpdateStatus(entity);
        }
        
        await _kubernetesClient.Ensure(() => new V1Namespace(), entity.Metadata.Name, "default");
        
        await _kubernetesClient.Ensure(() => new TenancyContext
        {
            Spec = new TenancyContextSpec()
            {
                OrganizationNamespace = entity.Metadata.NamespaceProperty
            }
        }, TenancyContext.GetName(), entity.Metadata.Name);

        /*
                         [FleetCluster.Name()] = entity.Name(),
                [Zone.CloudLabel()] = zone.Spec.Cloud,
                [] = zone.Spec.Environment,
                [Zone.RegionLabel()] = zone.Spec.Region,
                [Zone.ZoneLabel()] = zone.Metadata.Name,
                [Zone.EnvironmentTypeLabel()] = zone.Status.Type.ToString()
         */
        
        
        var attribute = await _kubernetesClient.Get<ZoneAttribute>(entity.Name(), entity.Namespace());
        if (attribute == null)
        {
            await _kubernetesClient.Create(() => new ZoneAttribute()
            {
                Metadata = new V1ObjectMeta()
                {
                    Labels = new Dictionary<string, string>()
                    {
                        { Zone.CloudLabel(), entity.Spec.Cloud },
                        { Zone.EnvironmentLabel(), entity.Spec.Environment },
                        { Zone.RegionLabel(), entity.Spec.Region },
                        { Zone.ZoneLabel(), entity.Name() },
                        { Zone.EnvironmentTypeLabel(), env.Spec.Type.ToString() },
                    }
                }
            }, entity.Name(), entity.Namespace());
        }
        else
        {
            attribute.Metadata.Labels[Zone.CloudLabel()] = entity.Spec.Cloud;
            attribute.Metadata.Labels[Zone.EnvironmentLabel()] = entity.Spec.Environment;
            attribute.Metadata.Labels[Zone.RegionLabel()] = entity.Spec.Region;
            attribute.Metadata.Labels[Zone.ZoneLabel()] = entity.Name();
            attribute.Metadata.Labels[Zone.EnvironmentTypeLabel()] = env.Spec.Type.ToString();
        }

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

    protected override async Task InternalDeletedAsync(Zone entity)
    {
        var local = "fleet-local";
        var @default = "fleet-default";       
        
        await _kubernetesClient.Delete<ZoneAttribute>(entity.Name(), entity.Namespace());
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), local);
        await _kubernetesClient.Delete<GitRepo>(entity.Name(), @default);
        await _kubernetesClient.Delete<V1Namespace>(entity.Metadata.Name, entity.Metadata.NamespaceProperty);
    }
}