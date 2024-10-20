namespace Dev.Controllers.Kubernetes.FireFighting;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Infrastructure;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;
using v1.Core.Services;
using v1.Core.Tenancies;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External;
using Account = v1.Core.Account;
using Project = v1.Platform.Rancher.Project;
using User = v1.Platform.Github.User;
using RancherUser = Dev.v1.Platform.Rancher.User;

[EntityRbac(typeof(FireFighter), Verbs = RbacVerb.All)]
public class FireFighterController : IResourceController<FireFighter>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<FireFighterController> _logger;

    public FireFighterController(        
        IKubernetesClient kubernetesClient,
        ILogger<FireFighterController> logger)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }
    
    public async Task<ResourceControllerResult?> ReconcileAsync(FireFighter? entity)
    {
        if (entity == null) return null;
        var spec = entity.Spec;
        var stat = entity.Status;

        if (stat.State != State.Activated)
        {
            return null;
        }
        var tenancy = spec.Tenancy;
        
        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), tenancy);
        if (context == null) throw new Exception($"cannot find tenancy context for {tenancy}");
        
        var rancher = await _kubernetesClient.Get<Rancher>("rancher", context.Spec.OrganizationNamespace);
        if (rancher == null) throw new Exception($"cannot find Rancher in {context.Spec.OrganizationNamespace}");
        
        var account = await _kubernetesClient.Get<v1.Core.Account>(spec.Account, context.Spec.OrganizationNamespace);
        if (account == null) throw new Exception($"cannot find account for {spec.Account}");
        
        var githubUser = await _kubernetesClient.Get<User>(account.Name(), account.Namespace());
        if (githubUser == null) throw new Exception($"cannot find github user for {spec.Account}");
        
        // var rancherUser = await _kubernetesClient.Get<RancherUser>(account.Name(), account.Namespace());
        // if (rancherUser == null) throw new Exception($"cannot find rancher user for {spec.Account}");

        var projects = await _kubernetesClient.List<Project>(null, 
        new EqualsSelector(Tenancy.TenancyLabel(), tenancy), 
        new EqualsSelector(Zone.EnvironmentTypeLabel(), EnvironmentType.Production.ToString()));

        
        foreach (var project in projects)
        {
            if(project.Status.Id == null) continue;
            
                await _kubernetesClient.Create(() =>  
                {
                    var b = ProjectRoleTemplateBinding.InitUser(
                        rancher.Spec.TechnicalUser, 
                        project.Spec.Kubernetes,
                        project.Status.Id, 
                        account.Name(),
                        account.Name(),
                        githubUser.Status.GithubId,
                        rancher.Spec.TenancyFireFighterMemberRole);
                    
                    b.Metadata.Labels.Add(FireFighter.FireFighterLabel(), entity.Name());
                    b.Metadata.Labels.Add(Account.AccountLabel(), account.Name());
                    b.Metadata.Labels.Add(Project.ProjectLabel(), project.Status.Id);
                    b.Metadata.Labels.Add(Tenancy.TenancyLabel(), tenancy);
                    
                    return b;
                }, null, project.Spec.Kubernetes);
            
        }

        return null;
    }

    public async Task DeletedAsync(FireFighter? entity)
    {
        if (entity == null) return;
        var spec = entity.Spec;
        var tenancy = spec.Tenancy;

        var bindings = await _kubernetesClient.List<ProjectRoleTemplateBinding>(null,
            new EqualsSelector(Tenancy.TenancyLabel(), tenancy),
            new EqualsSelector(Account.AccountLabel(), entity.Spec.Account),
            new EqualsSelector(FireFighter.FireFighterLabel(), entity.Name()));
        
        await _kubernetesClient.Delete(bindings);
    }
}