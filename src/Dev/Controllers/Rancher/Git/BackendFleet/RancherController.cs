namespace Dev.Controllers.Rancher.Git.BackendFleet;

using Dev.v1.Core;
using Dev.v1.Platform.Rancher;
using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;

[EntityRbac(typeof(Rancher), Verbs = RbacVerb.All)]
public class RancherController : IResourceController<Rancher>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly Templating _templating;
    private readonly ILogger<RancherController> _logger;

    public RancherController( 
        IKubernetesClient kubernetesClient,
        GitService gitService,
        Templating templating,
        ILogger<RancherController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _templating = templating;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    {
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");

        var orgNs = org.Metadata.NamespaceProperty;
        var templatesBase = "Controllers/Rancher/Git/BackendFleet/Org";
        
        var @default = "fleet-default";

        using var gitScope = await _gitService.BeginScope(@default, orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //generic permission (Cluster Roles) to be available in all clusters
            var content = _templating.Render(Path.Combine(templatesBase, "org-roles.yaml"));
            gitScope.EnsureFile("org/org-roles.yaml", content);
            
            content = _templating.Render(Path.Combine(templatesBase, "service-roles.yaml"));
            gitScope.EnsureFile("org/service-roles.yaml", content);

            gitScope.Commit("updated the org");
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