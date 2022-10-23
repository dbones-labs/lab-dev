namespace Dev.Controllers.Rancher.Git.ZoneDefaults;

using Dev.Controllers.Rancher.Git;
using Dev.v1.Core;
using Dev.v1.Core.Services;
using DotnetKubernetesClient;
using Infrastructure.Git;
using Infrastructure.Templates;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using TenancyDefaults;

/// <summary>
/// figures up the tenancies across the zones (which zone components will act on)
/// </summary>
[EntityRbac(typeof(Zone), Verbs = RbacVerb.All)]
public class ZoneController : IResourceController<Zone>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<TenancyController> _logger;

    public ZoneController(
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

    public async Task<ResourceControllerResult?> ReconcileAsync(Zone? entity)
    {
        if (entity == null) return null;
        if (entity.Name() == "control") return null;


            var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        
        var orgNs = org.Metadata.NamespaceProperty;
        var templatesBase = "Controllers/Rancher/Git/ZoneDefaults/Files";
        
        using var gitScope = await _gitService.BeginScope(entity.Name(), orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //setup the default files to enforce structure
            var content = _templating.Render(Path.Combine(templatesBase, "clusters.md"), new { Namespace = entity.Name() });
            gitScope.EnsureFile($"clusters/clusters.md", content);

            content = _templating.Render(Path.Combine(templatesBase, "cd.md"));
            gitScope.EnsureFile($"cd/cd.md", content);

            gitScope.Commit($"updated zone - {entity.Name()}");
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
}