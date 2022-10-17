namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class GlobalRoleBinding : CustomKubernetesEntity
{
    public static GlobalRoleBinding InitGroup(
        string creator,
        string tenancy,
        int groupPrincipal, 
        string role,
        string roleId)
    {
        var r = new GlobalRoleBinding
        {
            Metadata = new V1ObjectMeta
            {
                Annotations = new Dictionary<string, string>
                {
                    { "field.cattle.io/creatorId", creator },
                    { "cleanup.cattle.io/grbUpgradeCluster", "true" },
                    { "lifecycle.cattle.io/create.mgmt-auth-grb-controller", "true" }
                },
                Labels = new Dictionary<string, string>
                {
                    { Tenancy(), tenancy }
                },
                Finalizers = new List<string>
                {
                    "controller.cattle.io/mgmt-auth-grb-controller"
                },
                GenerateName = "grb-",
                OwnerReferences = new List<V1OwnerReference>
                {
                   new()
                   {
                       ApiVersion = "management.cattle.io/v3",
                       Kind = "GlobalRole",
                       Name = role,
                       Uid = roleId
                   } 
                }
            }
            
        };
        
        r.GlobalRoleName = role;
        r.GroupPrincipalName = $"github_team://{groupPrincipal}";
        
        return r;
    }
    

    public static string Tenancy() => "rancher.lab.dev/tenancy";
    
    /// <summary>
    /// unique role name with hyphens lowercase
    /// </summary>
    [Required]
    public string GlobalRoleName { get; set; }
    
    /// <summary>
    /// github_team://number
    /// </summary>
    public string GroupPrincipalName { get; set; }
}

