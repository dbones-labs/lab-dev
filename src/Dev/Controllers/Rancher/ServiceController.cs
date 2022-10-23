namespace Dev.Controllers.Rancher;

using v1.Core;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using Infrastructure;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core.Services;
using v1.Platform.Rancher.External.Fleet;

/// <summary>
/// setup the service in lab
/// </summary>
[EntityRbac(typeof(Service), Verbs = RbacVerb.All)]
public class ServiceController : IResourceController<Service>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(
        IKubernetesClient kubernetesClient,
        ILogger<ServiceController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Service? entity)
    {
        if (entity == null) return null;
        await _kubernetesClient.Ensure(() => new V1Namespace(), entity.Name());
        return null;
    }


    public async Task DeletedAsync(Service? entity)
    {
        if (entity == null) return;
        await _kubernetesClient.Delete<V1Namespace>(entity.Namespace());
    }
}