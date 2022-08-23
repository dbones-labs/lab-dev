namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class ProjectRoleTemplateBinding : CustomKubernetesEntity
{
    public static ProjectRoleTemplateBinding InitGroup(
        string creator, 
        string cluster, 
        string project,
        string tenancy,
        int groupPrincipal, 
        string role)
    {
        var r = new ProjectRoleTemplateBinding
        {
            Metadata = new V1ObjectMeta
            {
                Annotations = new Dictionary<string, string>
                {
                    { "field.cattle.io/creatorId", creator },
                    { $"lifecycle.cattle.io/create.cluster-prtb-sync_{cluster}", "true" },
                    { "lifecycle.cattle.io/create.mgmt-auth-prtb-controller", "true" }
                },
                Labels = new Dictionary<string, string>
                {
                    { "auth.management.cattle.io/crb-rb-labels-updated", "true" },
                    { "authz.cluster.cattle.io/crb-rb-labels-updated", "true" },
                    { RoleTenancy(), tenancy }
                },
                Finalizers = new List<string>
                {
                    $"clusterscoped.controller.cattle.io/cluster-prtb-sync_{cluster}",
                    "controller.cattle.io/mgmt-auth-prtb-controller"
                },
                GenerateName = "prtb-"
            }
            
        };
        
        r.ProjectName = $"{cluster}:{project}";
        r.RoleTemplateName = role;
        r.GroupPrincipalName = $"github_team://{groupPrincipal}";
        
        return r;
    }
    
    
    public static ProjectRoleTemplateBinding InitUser(
        string creator, 
        string cluster, 
        string project,
        string user,
        string displayName,
        string userPrincipal, 
        string role)
    {
        var r = new ProjectRoleTemplateBinding
        {
            Metadata = new V1ObjectMeta
            {
                Annotations = new Dictionary<string, string>
                {
                    { "auth.cattle.io/principal-display-name", displayName },
                    { "field.cattle.io/creatorId", creator },
                    { $"lifecycle.cattle.io/create.cluster-prtb-sync_{cluster}", "true" },
                    { "lifecycle.cattle.io/create.mgmt-auth-prtb-controller", "true" }
                },
                Labels = new Dictionary<string, string>
                {
                    { "auth.management.cattle.io/crb-rb-labels-updated", "true" },
                    { "authz.cluster.cattle.io/crb-rb-labels-updated", "true" },
                    { RoleTenancy(), user }
                },
                Finalizers = new List<string>
                {
                    $"clusterscoped.controller.cattle.io/cluster-prtb-sync_{cluster}",
                    "controller.cattle.io/mgmt-auth-prtb-controller"
                },
                GenerateName = "prtb-"
            }
            
        };
        
        r.ProjectName = $"{cluster}:{project}";
        r.RoleTemplateName = role;
        r.UserName = user;
        r.UserPrincipalName = $"github_team://{userPrincipal}";
        
        return r;
    }

    public static string RoleTenancy() => "rancher.lab.dev/zone-member";
    
    /// <summary>
    /// cluster:project
    /// </summary>
    [Required]
    public string ProjectName { get; set; }

    /// <summary>
    /// the name of the role to apply to the the principal
    /// </summary>
    [Required]
    public string RoleTemplateName { get; set; }

    /// <summary>
    /// github_team://number
    /// </summary>
    public string GroupPrincipalName { get; set; }

    
    /// <summary>
    /// the rancher id for the user - u-whzq6t3pkh
    /// </summary>
    public string UserName { get; set; }
    
    /// <summary>
    /// github_user://number
    /// </summary>
    public string UserPrincipalName { get; set; }
}

