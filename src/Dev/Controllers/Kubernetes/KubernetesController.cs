namespace Dev.Controllers.Kubernetes;

using DotnetKubernetesClient;
using Github.Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using Cluster = Dev.v1.Components.Kubernetes.Kubernetes;
using RancherCluster = v1.Platform.Rancher.External.Cluster;
using FleetCluster = v1.Platform.Rancher.External.Fleet.Cluster;

[EntityRbac(typeof(Cluster), Verbs = RbacVerb.All)]
public class KubernetesController : IResourceController<Cluster>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<KubernetesController> _logger;

    public KubernetesController(        
        IKubernetesClient kubernetesClient,
        ILogger<KubernetesController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(Cluster? entity)
    {
        if (entity == null) return null;

        var fleetNamespace = "fleet-default";
        var clusterName = entity.Metadata.Name;
        var zoneName = entity.Metadata.NamespaceProperty;

        var fleetCluster = await _kubernetesClient.Get<FleetCluster>(entity.Metadata.Name, fleetNamespace);
        if (fleetCluster == null) throw new Exception($"cannot find fleet cluster {zoneName}/{clusterName}");

        if(!fleetCluster.Metadata.Labels.TryGetValue(FleetCluster.IdLabel(), out var clusterId))
            throw new Exception($"cannot find cluster id {zoneName}/{clusterName}");
        
        var rancherCluster = await _kubernetesClient.Get<RancherCluster>(clusterId);
        if (rancherCluster == null) throw new Exception($"cannot find rancher cluster {zoneName}/{clusterName} - {clusterId}");

        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), zoneName);
        if (context == null) throw new Exception($"cannot find tenancy context for {zoneName}");

        var zone = await _kubernetesClient.Get<Zone>(zoneName, context.Spec.OrganizationNamespace);
        if (zone == null) throw new Exception($"cannot find zone {zoneName}");
        
        //we need to update all of these due to the zone.
        
        var fl = fleetCluster.Metadata.Labels;
        await _kubernetesClient.UpsertCrdLabel(fleetCluster, new Dictionary<string, string?>(fl)
        {
            [Zone.CloudLabel()] = zone.Spec.Cloud,
            [Zone.EnvironmentLabel()] = zone.Spec.Environment,
            [Zone.RegionLabel()] = zone.Spec.Region,
            [Zone.ZoneLabel()] = zone.Metadata.Name,
            [Zone.ProductionLabel()] = zone.Status.IsProduction.ToString()
        });
        
        var rl = rancherCluster.Metadata.Labels;
        await _kubernetesClient.UpsertCrdLabel(rancherCluster, new Dictionary<string, string?>(rl)
        {
            [Zone.CloudLabel()] = zone.Spec.Cloud,
            [Zone.EnvironmentLabel()] = zone.Spec.Environment,
            [Zone.RegionLabel()] = zone.Spec.Region,
            [Zone.ZoneLabel()] = zone.Metadata.Name,
            [Zone.ProductionLabel()] = zone.Status.IsProduction.ToString()
        });
        
        //store this info in the state, as this will be in conflict with Gitops if we stored it in the labels.
        entity.Status.ClusterId = clusterId;
        entity.Status.Cloud = zone.Spec.Cloud;
        entity.Status.Environment = zone.Spec.Environment;
        entity.Status.Region = zone.Spec.Region;
        entity.Status.IsProduction = zone.Status.IsProduction;
        await _kubernetesClient.UpdateStatus(entity);

        return null;
    }
    
    //intentional no delete
}