namespace Dev.Controllers.Rancher;

using DotnetKubernetesClient;
using Github.Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Platform.Github;
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

    public async Task<ResourceControllerResult?> ReconcileAsync(Rancher? entity)
    {
        if (entity == null) return null;
        if (entity.Metadata.Name != "rancher") throw new Exception("please call the Rancher Resource - 'rancher'");

        var orgs = await _kubernetesClient.List<Organization>(entity.Metadata.NamespaceProperty);
        var org = orgs.FirstOrDefault();
        if (org == null) throw new Exception("please ensure you add an Organisation");

        var orgNs = org.Metadata.NamespaceProperty;
        
        //fleet
        //setup for the ORG
        //setup for the repo which we will setup each tenancy later.
        
        //needed to store the the gitops for handling access for downstream repo's
        await _kubernetesClient.Ensure(() => new Repository()
        {
            Spec = new()
            {
                EnforceCollaborators = false,
                State = State.Active,
                Type = Type.System,
                Visibility = Visibility.Private,
                OrganizationNamespace = orgNs
            }
        }, "fleet", orgNs);
        
        
        var github = await _kubernetesClient.GetGithub(orgNs);
        var token = await _kubernetesClient.GetSecret(github.Metadata.NamespaceProperty, github.Spec.Credentials);
        
        return null;
    }
}