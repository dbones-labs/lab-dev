namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Platform.Rancher;

[EntityRbac(typeof(Rancher), Verbs = RbacVerb.All)]
public class RancherController : IResourceController<Rancher>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<RancherController> _logger;

    public RancherController( 
        IKubernetesClient kubernetesClient,
        ILogger<RancherController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    // public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    // {
    //     
    // }
}