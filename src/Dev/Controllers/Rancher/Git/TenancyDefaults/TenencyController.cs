namespace Dev.Controllers.Rancher.Git.TenancyDefaults;

using Dev.Controllers.Rancher.Git;
using Dev.v1.Core;
using DotnetKubernetesClient;
using Infrastructure;
using Infrastructure.Caching;
using Infrastructure.Git;
using Infrastructure.Templates;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;

/// <summary>
/// figures up the tenancies across the zones (which zone components will act on)
/// </summary>
[EntityRbac(typeof(Tenancy), Verbs = RbacVerb.All)]
public class TenancyController : ResourceController<Tenancy>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<TenancyController> _logger;

    public TenancyController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        GitService gitService,
        Templating templating,
        ILogger<TenancyController> logger
        ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _templating = templating;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Tenancy entity)
    {
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");
        
        var orgNs = org.Metadata.NamespaceProperty;
        var templatesBase = "Controllers/Rancher/Git/TenancyDefaults/Files";
        
        
        using var gitScope = await _gitService.BeginScope(entity.Name(), orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //setup the default files to enforce structure
            var content = _templating.Render(Path.Combine(templatesBase, "members.md"), new { Namespace = entity.Name() });
            gitScope.EnsureFile($"members/members.md", content);
            
            content = _templating.Render(Path.Combine(templatesBase, "services.md"), new { Namespace = entity.Name() });
            gitScope.EnsureFile($"services/services.md", content);
            
            content = _templating.Render(Path.Combine(templatesBase, "libraries.md"), new { Namespace = entity.Name() });
            gitScope.EnsureFile($"libraries/libraries.md", content);
            
            content = _templating.Render(Path.Combine(templatesBase, "cd.md"));
            gitScope.EnsureFile($"cd/cd.md", content);

            gitScope.Commit($"updated tenancy - {entity.Name()}");
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