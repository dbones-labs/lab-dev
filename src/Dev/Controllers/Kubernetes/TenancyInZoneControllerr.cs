namespace Dev.Controllers.Kubernetes;

using v1.Core.Services;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Platform.Github;
using v1.Platform.Rancher;
using Cluster = Dev.v1.Components.Kubernetes.Kubernetes;

[EntityRbac(typeof(TenancyInZone), Verbs = RbacVerb.All)]
public class TenancyInZoneController : ResourceController<TenancyInZone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TenancyInZoneController> _logger;

    public TenancyInZoneController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<TenancyInZoneController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(TenancyInZone entity)
    {
        //setup project in the zone
        var zoneName = entity.Metadata.NamespaceProperty;
        var tenancyName = entity.Spec.Tenancy;
        
        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), zoneName);
        if (context == null) throw new Exception($"cannot find tenancy context for {zoneName}");

        var zone = await _kubernetesClient.Get<Zone>(zoneName, context.Spec.OrganizationNamespace);
        if (zone == null) throw new Exception($"cannot find zone {zoneName}");

        //need to create a project in all clusters of the zxone
        var clusters = await _kubernetesClient.List<Cluster>(zoneName);

        foreach (var cluster in clusters)
        {
            ////if the cluster is still being setup, we will get it on the next cycle.
            //if (string.IsNullOrWhiteSpace(cluster.Status.ClusterId)) continue;
            
            var project = _kubernetesClient.Ensure(() => new Project()
            {
                Metadata = new V1ObjectMeta()
                {
                    Labels = new Dictionary<string, string>
                    {
                        { Tenancy.TenancyLabel(), tenancyName },
                        { Zone.EnvironmentLabel(), cluster.Status.Environment },
                        { Zone.EnvironmentTypeLabel(), cluster.Status.Type.ToString() }
                    }
                },
                Spec = new ProjectSpec()
                {
                    Kubernetes = cluster.Metadata.Name,
                    Tenancy = tenancyName
                }
            }, $@"{cluster.Metadata.Name}.{tenancyName}", zoneName);
        }
        
        return null;
    }
    
    protected override async Task InternalDeletedAsync(TenancyInZone entity)
    {
        var zoneName = entity.Metadata.NamespaceProperty;
        var tenancySelector = new EqualsSelector(Tenancy.TenancyLabel(), entity.Spec.Tenancy);

        var projects = await _kubernetesClient
            .List<Project>(
                zoneName, 
                tenancySelector
                );
        
        await _kubernetesClient.Delete(projects);
    }
}