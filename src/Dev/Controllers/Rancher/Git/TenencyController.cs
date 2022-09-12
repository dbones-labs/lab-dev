namespace Dev.Controllers.Rancher.Git;

using v1.Core;
using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;

/// <summary>
/// figures up the tenancies across the zones (which zone components will act on)
/// </summary>
[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : IResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        IKubernetesClient kubernetesClient,
        GitService gitService,
        Templating templating,
        ILogger<TenancyController> logger
        )
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _templating = templating;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Tenancy? entity)
    {
        if (entity == null) return null;
        
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        
        var orgNs = org.Metadata.NamespaceProperty;
        var templatesBase = "Controllers/Rancher/Git/Tenancies";
        
        var @default = "fleet-default";
        
        using var gitScope = await _gitService.BeginScope(@default, orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //attach the tenancy so it can deploy to a tenancy (this only happens once per cluster)
            var content = _templating.Render(Path.Combine(templatesBase, "tenancy.yaml"), new { Name = entity.Name() });
            gitScope.EnsureFile($"tenancies/{entity.Name()}.yaml", content);

            gitScope.Commit($"updated tenancy - {entity.Name()}");
            gitScope.Push("main");
        }
        catch (Exception ex)
        {
            gitScope.CleanUp();
            throw;
        }
        
        return null;
    }


    public async Task DeletedAsync(Tenancy? entity)
    {
        if (entity == null) return;
        
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        var orgNs = org.Metadata.NamespaceProperty;
        
        var @default = "fleet-default";
        
        using var gitScope = await _gitService.BeginScope(@default, orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //attach the tenancy so it can deploy to a tenancy (this only happens once per cluster)
            gitScope.RemoveFile($"./tenancies/{entity.Name()}.yaml");

            gitScope.Commit($"removed tenancy - {entity.Name()}");
            gitScope.Push("main");
        }
        catch (Exception)
        {
            gitScope.CleanUp();
            throw;
        }
    }
}