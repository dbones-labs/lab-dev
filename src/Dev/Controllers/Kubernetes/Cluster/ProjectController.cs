namespace Dev.Controllers.Kubernetes.Internal;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Components.Kubernetes;
using v1.Core;
using v1.Platform.Github;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External;
using Project = v1.Platform.Rancher.Project;
using RancherProject = v1.Platform.Rancher.External.Project;

[EntityRbac(typeof(Project), Verbs = RbacVerb.All)]
public class ProjectController : IResourceController<Project>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        IKubernetesClient kubernetesClient,
        ILogger<ProjectController> logger
    )
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    public async Task<ResourceControllerResult?> ReconcileAsync(Project? entity)
    {
        if (entity == null) return null;

        var zoneName = entity.Metadata.NamespaceProperty;

        var context = await _kubernetesClient.Get<TenancyContext>(TenancyContext.GetName(), zoneName);
        if (context == null) throw new Exception($"cannot find tenancy context for {zoneName}");

        var rancher = await _kubernetesClient.Get<Rancher>("rancher", context.Spec.OrganizationNamespace);
        if (rancher == null) throw new Exception($"cannot find Rancher in {context.Spec.OrganizationNamespace}");

        var cluster = await _kubernetesClient.Get<Kubernetes>(entity.Spec.Kubernetes, zoneName);
        if (cluster == null) throw new Exception($"cannot find cluster {entity.Spec.Kubernetes}");
        if (string.IsNullOrWhiteSpace(cluster.Status.ClusterId))
            throw new Exception($"id has not been associated with cluster {entity.Spec.Kubernetes}");


        var label = $"{cluster.Status.ClusterId}_{entity.Metadata.Name}";

        if (string.IsNullOrEmpty(entity.Status.Id))
        {
            var selector = new EqualsSelector(Project.ProjectLabel(), label);
            var projects = await _kubernetesClient.List<RancherProject>(cluster.Status.ClusterId, selector);

            var rancherProject = projects.FirstOrDefault() ?? await _kubernetesClient.Create(() =>
            {
                var p = new RancherProject();
                RancherProject.Init(
                    p,
                    entity.Spec.Tenancy,
                    rancher.Spec.TechnicalUser,
                    cluster.Status.ClusterId);
                
                p.Metadata.Labels ??= new Dictionary<string, string>();
                p.Metadata.Labels.Add(Project.ProjectLabel(), label);

                return p;
            }, null, cluster.Status.ClusterId);

            entity.Status.Id = rancherProject.Metadata.Name;
            await _kubernetesClient.UpdateStatus(entity);
        }

        //setup the Github team to have access

        //EXCEPTION TO A RULE
        //as im unsure how to query k8s for github groups/teams
        var tenancy = entity.Spec.Tenancy;
        var github = await _kubernetesClient.GetGithub(entity.Metadata.NamespaceProperty);
        var name = github.IsGlobal(tenancy)
            ? context.Spec.OrganizationNamespace // repo <- global team
            : Team.GetTeamName(tenancy); // repo <- tenancy team(-guest)


        var team = await _kubernetesClient.Get<Team>(tenancy, name);
        if (team == null) throw new Exception($"cannot find github team for {entity.Spec.Tenancy}");
        if (!team.Status.Id.HasValue) throw new Exception($"github team is not synced (yet) - {entity.Spec.Tenancy}");
        
        var roleName = cluster.Status.IsProduction
            ? rancher.Spec.TenancyProductionMemberRole
            : rancher.Spec.TenancyMemberRole;

        //check, add or update....
        var bindingSelector = new EqualsSelector(ProjectRoleTemplateBinding.RoleTenancy(), tenancy);
        var bindings = await _kubernetesClient.List<ProjectRoleTemplateBinding>(entity.Status.Id, bindingSelector);
        var binding = bindings.FirstOrDefault();
        if (binding == null)
        {
            await _kubernetesClient.Create(() =>  
            {
                var b = ProjectRoleTemplateBinding.InitGroup(
                    rancher.Spec.TechnicalUser, 
                    cluster.Status.ClusterId,
                    entity.Status.Id, 
                    tenancy,
                    team.Status.Id.Value,
                    roleName);
                return b;
            }, null, entity.Status.Id);
        }

        return null;
    }

    public async Task DeletedAsync(Project? entity)
    {
        if (entity == null) return;
        var zoneName = entity.Metadata.NamespaceProperty;

        var cluster = await _kubernetesClient.Get<Kubernetes>(entity.Spec.Kubernetes, zoneName);
        if (cluster == null) throw new Exception($"cannot find cluster {entity.Spec.Kubernetes}");
        if (string.IsNullOrWhiteSpace(cluster.Status.ClusterId))
            throw new Exception($"id has not been associated with cluster {entity.Spec.Kubernetes}");

        var label = $"{cluster.Status.ClusterId}/{entity.Metadata.Name}";

        var selector = new EqualsSelector(Project.ProjectLabel(), label);

        var rancherProjects = await _kubernetesClient
            .List<RancherProject>(
                cluster.Status.ClusterId, //entity.Metadata.NamespaceProperty, 
                selector);

        var rancherProject = rancherProjects.FirstOrDefault();
        if (rancherProject != null) await _kubernetesClient.Delete(rancherProject);;

        var bindingSelector = new EqualsSelector(ProjectRoleTemplateBinding.RoleTenancy(), entity.Spec.Tenancy);
        var bindings = await _kubernetesClient.List<ProjectRoleTemplateBinding>(entity.Status.Id, bindingSelector);
        var binding = bindings.FirstOrDefault();
        if (binding != null) await _kubernetesClient.Delete(binding);
    }
}