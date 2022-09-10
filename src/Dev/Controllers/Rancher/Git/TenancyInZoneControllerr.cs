namespace Dev.Controllers.Rancer.Git;

using System.Reflection.Metadata.Ecma335;
using v1.Core.Services;
using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Rancher.Git;
using v1.Core;
using v1.Platform.Rancher;
using Cluster = Dev.v1.Components.Kubernetes.Kubernetes;

[EntityRbac(typeof(Service), Verbs = RbacVerb.All)]
public class ServiceInClusterController : IResourceController<Service>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<ServiceInClusterController> _logger;

    public ServiceInClusterController(
        IKubernetesClient kubernetesClient,
        GitService gitService,
        Templating templating,
        ILogger<ServiceInClusterController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _templating = templating;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Service? entity)
    {
        if (entity == null) return null;
     
        //setup project in the zone
        var tenancyName = entity.Metadata.NamespaceProperty;
        
        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), tenancyName);
        if (context == null) throw new Exception($"cannot find tenancy context for {tenancyName}");
        var orgNs = context.Spec.OrganizationNamespace;
        
        var templatesBase = "Controllers/Rancher/Git/Services";
        
        using var gitScope = await _gitService.BeginScope("fleet", orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            foreach (var zone in entity.Spec.Zones)
            {
                var zoneResource = await _kubernetesClient.Get<Zone>(zone.Name, orgNs);
                if (zoneResource == null)
                {
                    _logger.LogError("cannot find zone {Zone}", zone.Name);
                    continue;
                }
                
                foreach (var kubernetes in zone.Components.Where(x=> x.Provider == "kubernetes"))
                {
                    
                    var content = _templating.Render(Path.Combine(templatesBase, "fleet.yaml"), new
                    {
                        Environment = zoneResource.Spec.Environment,
                        Name = kubernetes.Name
                    });
                    gitScope.EnsureFile($"{zoneResource.Spec.Environment}/kubernetes/{kubernetes.Name}/fleet.yaml", content);
                    
                    
                    // var kubernetesResource = await _kubernetesClient.Get<>()
                    //
                    // content = _templating.Render(Path.Combine(templatesBase, "fleet.yaml"), new
                    // {
                    //     projectId = zoneResource.Spec.Environment,
                    //     kubernetes = kubernetes.Name
                    // });
                    //
                }
            }
            
            //gitScope.Commit($"updated service - {entity.Name()}");
            //gitScope.Push("main");
        }
        catch (Exception ex)
        {
            gitScope.CleanUp();
            throw;
        }
        
        
        
        
        //need to create a project in all clusters of the zxone
        //var clusters = await _kubernetesClient.List<Cluster>(zoneName);

        
        return null;
    }
    
    public async Task DeletedAsync(Service? entity)
    {
        if (entity == null) return;
        
        
    }
}