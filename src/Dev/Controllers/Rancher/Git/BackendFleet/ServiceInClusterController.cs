namespace Dev.Controllers.Rancher.Git.BackendFleet;

using Dev.Controllers.Rancher.Git;
using Dev.v1.Core;
using Dev.v1.Core.Services;
using Dev.v1.Platform.Rancher;
using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using Infrastructure.Git;
using Infrastructure.Templates;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Cluster = v1.Components.Kubernetes.Kubernetes;

[EntityRbac(typeof(Service), Verbs = RbacVerb.All)]
public class ServiceInClusterController : ResourceController<Service>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<ServiceInClusterController> _logger;

    public ServiceInClusterController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        GitService gitService,
        Templating templating,
        ILogger<ServiceInClusterController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _templating = templating;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Service entity)
    {
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
                        Zone = zone.Name,
                        Cluster = kubernetes.Name
                    });
                    gitScope.EnsureFile($"{zone.Name}/kubernetes/{kubernetes.Name}/fleet.yaml", content);


                    var cluster = await _kubernetesClient.Get<Cluster>(kubernetes.Name, zone.Name);
                    var project = await _kubernetesClient.Get<Project>($@"{cluster.Metadata.Name}.{tenancyName}", zone.Name);
                    if (project == null)
                    {
                        _logger.LogError($"{tenancyName} does not have access to {cluster.Name()}");
                        continue;
                    }


                    content = _templating.Render(Path.Combine(templatesBase, "service.yaml"), new
                    {
                        Project = project.Status.Id,
                        Kubernetes = cluster.Status.ClusterId,
                        Service = entity.Name(),
                        Tenancy = entity.Namespace()
                    });
                    gitScope.EnsureFile($"{zone.Name}/kubernetes/{kubernetes.Name}/overlays/deploy/{entity.Name()}.yaml", content);
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
    
    protected override async Task InternalDeletedAsync(Service entity)
    {
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
                    gitScope.RemoveFile($"{zone.Name}/kubernetes/{kubernetes.Name}/{entity.Name()}.yaml");
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