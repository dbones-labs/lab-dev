namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class ClusterRoleTemplateBinding : CustomKubernetesEntity
{
    public static ClusterRoleTemplateBinding InitGroup(
        string cluster, 
        ProjectType type,
        string tenancy,
        int groupPrincipal, 
        string role)
    {
        var r = new ClusterRoleTemplateBinding
        {
            Metadata = new V1ObjectMeta
            {
                Annotations = new Dictionary<string, string>
                {
                    { $"lifecycle.cattle.io/create.cluster-crtb-sync_{cluster}", "true" },
                    { "lifecycle.cattle.io/create.mgmt-auth-crtb-controller", "true" }
                },
                Labels = new Dictionary<string, string>
                {
                    { "auth.management.cattle.io/crb-rb-labels-updated", "true" },
                    { "authz.cluster.cattle.io/crb-rb-labels-updated", "true" },
                    { Tenancy(), tenancy },
                    { MemberType(), type.ToString() }
                },
                Finalizers = new List<string>
                {
                    $"clusterscoped.controller.cattle.io/cluster-crtb-sync_{cluster}",
                    "controller.cattle.io/mgmt-auth-crtb-controller"
                },
                Name = $"{groupPrincipal}-{role}",
                NamespaceProperty = cluster
            }
            
        };
        
        r.ClusterName = cluster;
        r.RoleTemplateName = role;
        r.GroupPrincipalName = $"github_team://{groupPrincipal}";
        
        return r;
    }
    

    public static string Tenancy() => "rancher.lab.dev/tenancy";
    public static string MemberType() => "rancher.lab.dev/member-type";
    
    /// <summary>
    /// cluster:project
    /// </summary>
    [Required]
    public string ClusterName { get; set; }

    /// <summary>
    /// the name of the role to apply to the the principal
    /// </summary>
    [Required]
    public string RoleTemplateName { get; set; }

    /// <summary>
    /// github_team://number
    /// </summary>
    public string GroupPrincipalName { get; set; }
    
}

public enum ProjectType
{
    Zone,
    Tenancy
}