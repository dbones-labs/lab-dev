namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core.Services;


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

        if (entity.Status.IsProduction != env.Spec.IsProduction)
        {
            entity.Status.IsProduction = env.Spec.IsProduction;
            await _kubernetesClient.UpdateStatus(entity);
        }
        
        await _kubernetesClient.Ensure(() => new V1Namespace(), entity.Metadata.Name, entity.Metadata.NamespaceProperty);

        return null;
    }

    public async Task DeletedAsync(Zone? entity)
    {
        if (entity == null) return;

        await _kubernetesClient.Delete<V1Namespace>(entity.Metadata.Name, entity.Metadata.NamespaceProperty);
    }
}