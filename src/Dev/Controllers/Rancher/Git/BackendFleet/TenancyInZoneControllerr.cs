namespace Dev.Controllers.Rancher.Git.BackendFleet;

using Dev.Controllers.Rancher.Git;
using Dev.v1.Core;
using Dev.v1.Core.Services;
using Dev.v1.Platform.Rancher;
using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Cluster = v1.Components.Kubernetes.Kubernetes;

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
        
        
        //the concept here, is to provide the Tenancy access to the Service Namespace (and also create it if its missing)
     
        //setup project in the zone
        var tenancyName = entity.Metadata.NamespaceProperty;
        
        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), tenancyName);
        if (context == null) throw new Exception($"cannot find tenancy context for {tenancyName}");
        var orgNs = context.Spec.OrganizationNamespace;
        
        var templatesBase = "Controllers/Rancher/Git/BackendFleet/Services";
        
        var @default = "fleet-default";
        
        using var gitScope = await _gitService.BeginScope(@default, orgNs);
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
                        Cluster = kubernetes.Name
                    });
                    gitScope.EnsureFile($"{zoneResource.Spec.Environment}/kubernetes/{kubernetes.Name}/fleet.yaml", content);


                    var cluster = await _kubernetesClient.Get<Cluster>(kubernetes.Name, zone.Name);
                    var project = await _kubernetesClient.Get<Project>($@"{cluster.Metadata.Name}.{tenancyName}", zone.Name);
                    
                    content = _templating.Render(Path.Combine(templatesBase, "service.yaml"), new
                    {
                        ProjectId = project.Status.Id,
                        KubernetesId = cluster.Status.ClusterId,
                        Service = entity.Name(),
                        Tenancy = entity.Namespace()
                    });
                    gitScope.EnsureFile($"{zoneResource.Spec.Environment}/kubernetes/{kubernetes.Name}/{entity.Name()}.yaml", content);
                }
            }
            
            gitScope.Commit($"updated service - {entity.Name()}");
            gitScope.Push("main");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("git scope is")) return null; //refactor
            gitScope.CleanUp();
            throw;
        }
        
        return null;
    }
    
    public async Task DeletedAsync(Service? entity)
    {
        if (entity == null) return;
        
        //setup project in the zone
        var tenancyName = entity.Metadata.NamespaceProperty;
        
        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), tenancyName);
        if (context == null) throw new Exception($"cannot find tenancy context for {tenancyName}");
        var orgNs = context.Spec.OrganizationNamespace;
        
        var templatesBase = "Controllers/Rancher/Git/Services";
        
        var @default = "fleet-default";
        
        using var gitScope = await _gitService.BeginScope(@default, orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();

            foreach (var zone in entity.Spec.Zones)
            {
                var zoneResource = await _kubernetesClient.Get<Zone>(zone.Name, orgNs);
                if (zoneResource == null) throw new Exception($"cannot find zone {zone.Name}");

                foreach (var kubernetes in zone.Components.Where(x => x.Provider == "kubernetes"))
                {
                    gitScope.RemoveFile($"{zoneResource.Spec.Environment}/kubernetes/{kubernetes.Name}/{entity.Name()}.yaml");
                }
            }

            gitScope.Commit($"removing service - {entity.Name()}");
            gitScope.Push("main");
        }
        catch (Exception ex)
        {
            gitScope.CleanUp();
            throw;
        }
        
    }
}