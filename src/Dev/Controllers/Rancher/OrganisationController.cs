namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Github.Internal;
using Infrastructure;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;

[EntityRbac(typeof(Organization), Verbs = RbacVerb.All)]
public class OrganisationController : IResourceController<Organization>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<OrganisationController> _logger;

    public OrganisationController(
        IKubernetesClient kubernetesClient,
        ILogger<OrganisationController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Organization? entity)
    {
        if (entity == null) return null;

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