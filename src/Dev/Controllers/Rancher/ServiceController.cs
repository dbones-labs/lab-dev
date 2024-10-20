namespace Dev.Controllers.Rancher;

using v1.Core;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using Infrastructure;
using Infrastructure.Caching;
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
public class ServiceController : ResourceController<Service>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<ServiceController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Service entity)
    {
        await _kubernetesClient.Ensure(() => new V1Namespace(), entity.Name(), "default");
        return null;
    }


    protected override async Task InternalDeletedAsync(Service entity)
    {
        await _kubernetesClient.Delete<V1Namespace>(entity.Namespace(), "default");
    }
}