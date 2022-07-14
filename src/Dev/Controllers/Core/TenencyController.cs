namespace Dev.Controllers.Core;

using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;

[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : IResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        IKubernetesClient kubernetesClient,
        ILogger<TenancyController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Tenancy? entity)
    {
        if (entity == null) return null;

        var contextName = TenancyContext.GetName();
        var @namespace = Tenancy.GetNamespaceName(entity.Metadata.Name);
        
        var ns = await _kubernetesClient.Get<V1Namespace>(@namespace);
        if (ns == null)
        {
            ns = await _kubernetesClient.Create(new V1Namespace()
            {
                Metadata = new()
                {
                    Name = @namespace
                }
            });
            await _kubernetesClient.Create(ns);
        }

        var context = await _kubernetesClient.Get<TenancyContext>(contextName, @namespace);
        if (context == null)
        {
            context = new TenancyContext
            {
                Metadata = new()
                {
                    Name = contextName,
                    NamespaceProperty = @namespace
                },

                Spec = new()
                {
                    OrganizationNamespace = entity.Metadata.NamespaceProperty
                }
            };
            await _kubernetesClient.Create(context);
        }
        
        return null;
    }


    public async Task DeletedAsync(Tenancy? entity)
    {
        if (entity == null) return;
        
        var contextName = TenancyContext.GetName();
        var @namespace = Tenancy.GetNamespaceName(entity.Metadata.Name);

        await _kubernetesClient.Delete<TenancyContext>(contextName, @namespace);
        await _kubernetesClient.Delete<V1Namespace>(@namespace);
    }
}