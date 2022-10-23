namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;

[EntityRbac(typeof(Organization), Verbs = RbacVerb.All)]
public class OrganisationController : ResourceController<Organization>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<OrganisationController> _logger;

    public OrganisationController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<OrganisationController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Organization entity)
    {
        await _kubernetesClient.Ensure(() => new V1ConfigMap()
        {
            Data = new Dictionary<string, string>()
            {
                { "namespace", entity.Namespace() },
                { "name", entity.Name() }
            }
        }, "lab.dev", "default");

        return null;
    }
}