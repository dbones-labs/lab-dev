namespace Dev.Controllers.Rancher.Git;

using DotnetKubernetesClient;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Platform.Rancher;

[EntityRbac(typeof(Rancher), Verbs = RbacVerb.All)]
public class RancherController : IResourceController<Rancher>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly GitService _gitService;
    private readonly ILogger<RancherController> _logger;

    public RancherController( 
        IKubernetesClient kubernetesClient,
        GitService gitService,
        ILogger<RancherController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _gitService = gitService;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    {
        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");

        var orgNs = org.Metadata.NamespaceProperty;

        using var gitScope = await _gitService.BeginScope("fleet", orgNs);
        try
        {
            gitScope.Clone();
            gitScope.Fetch();
            
            //todo: this.
            //generic permission (Cluster Roles) to be available in all clusters
            gitScope.EnsureFile("./org/cluster-permissions.yaml", "yaml content");
            gitScope.EnsureFile("./org/namespace-permissions.yaml", "yaml content");

            gitScope.Commit("updated the org");
            gitScope.Push("main");
        }
        catch (Exception)
        {
            gitScope.CleanUp();
            throw;
        }
        
        return null;
    }
}