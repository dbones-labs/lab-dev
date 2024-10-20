namespace Dev.Controllers.Kubernetes.Internal;

using DotnetKubernetesClient;
using DotnetKubernetesClient.LabelSelectors;
using Github.Internal;
using Infrastructure;
using Infrastructure.Caching;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Components.Kubernetes;
using v1.Core;
using v1.Core.Services;
using v1.Platform.Github;
using v1.Platform.Rancher;
using v1.Platform.Rancher.External;
using Project = v1.Platform.Rancher.Project;
using RancherProject = v1.Platform.Rancher.External.Project;

[EntityRbac(typeof(Project), Verbs = RbacVerb.All)]
public class ProjectController : ResourceController<Project>
{
    private readonly IKubernetesClient _kubernetesClient;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        ResourceCache resourceCache,
        IKubernetesClient kubernetesClient,
        ILogger<ProjectController> logger
    ) : base(resourceCache)
    {
        _kubernetesClient = kubernetesClient;
        _logger = logger;
    }

    protected override async Task<ResourceControllerResult?> InternalReconcileAsync(Project entity)
    {
        var zoneName = entity.Namespace();

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
                var p = RancherProject.Init(
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
        
        
        //Tenancy Role --> Project
        var roleName = cluster.Status.Type == EnvironmentType.Production
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
        
        
        //Zone/cluster access
        var tenancyResource = await _kubernetesClient.Get<Tenancy>(tenancy, context.Spec.OrganizationNamespace);
        if (tenancyResource == null) throw new Exception($"cannot find Tenancy for {tenancy}");

        string clusterRoleName = null;
        if (tenancyResource.Spec.IsPlatform)
        {
            //Zone Role --> cluster
            clusterRoleName = cluster.Status.Type == EnvironmentType.Production
                ? rancher.Spec.ClusterProductionZoneMemberRole
                : rancher.Spec.ClusterZoneMemberRole;
        }
        else
        {
            //Tenancy Role --> cluster
            clusterRoleName = cluster.Status.Type == EnvironmentType.Production
                ? rancher.Spec.ClusterProductionTenancyMemberRole
                : rancher.Spec.ClusterTenancyMemberRole;
        }

        if (string.IsNullOrWhiteSpace(clusterRoleName)) return null;

        var clusterBindingSelector = new EqualsSelector(ClusterRoleTemplateBinding.Tenancy(), tenancy);
        var clusterBindings = await _kubernetesClient.List<ClusterRoleTemplateBinding>(cluster.Status.ClusterId, clusterBindingSelector);
        
        var desiredRoles = clusterRoleName.Split(",").Select(x => x.Trim()).ToHashSet();
        var existingRoles = clusterBindings.ToDictionary(k=> k.RoleTemplateName, v => v);
        var missingRoles = desiredRoles.Where(x => !existingRoles.Keys.Contains(x));
        var obsoleteRoles = existingRoles.Where(x => !desiredRoles.Contains(x.Key)).Select(x=>x.Value);

        foreach (var missingRole in missingRoles)
        {
            await _kubernetesClient.Create(() =>  
            {
                var b = ClusterRoleTemplateBinding.InitGroup(
                    cluster.Status.ClusterId,
                    ProjectType.Tenancy, 
                    tenancy,
                    team.Status.Id.Value,
                    missingRole);
                return b;
            }, null, "default");
        }
        
        if (clusterBindings.Any()) await _kubernetesClient.Delete(obsoleteRoles);

        return null;
    }

    protected override async Task InternalDeletedAsync(Project entity)
    {
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
        if (rancherProject != null) await _kubernetesClient.Delete(rancherProject);

        var bindingSelector = new EqualsSelector(ProjectRoleTemplateBinding.RoleTenancy(), entity.Spec.Tenancy);
        var bindings = await _kubernetesClient.List<ProjectRoleTemplateBinding>(entity.Status.Id, bindingSelector);
        var binding = bindings.FirstOrDefault();
        if (binding != null) await _kubernetesClient.Delete(binding);
        
        var clusterBindingSelector = new EqualsSelector(ClusterRoleTemplateBinding.Tenancy(), entity.Spec.Tenancy);
        var clusterBindings = await _kubernetesClient.List<ClusterRoleTemplateBinding>(cluster.Status.ClusterId, clusterBindingSelector);
        if (clusterBindings.Any()) await _kubernetesClient.Delete(clusterBindings);
    }
}