namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class Globalrole : CustomKubernetesEntity
{
    public static Globalrole Init(string name,string creator)
    {
        return new Globalrole()
        {
            Metadata = new V1ObjectMeta()
            {
                Annotations = new Dictionary<string, string>()
                {
                    { "authz.management.cattle.io/cr-name", $"cattle-globalrole-{name}" },
                    { "field.cattle.io/creatorId", creator },
                    { "lifecycle.cattle.io/create.mgmt-auth-gr-controller", "true" }
                },
                Finalizers = new List<string>()
                {
                    "controller.cattle.io/mgmt-auth-gr-controller"
                }
            }
        };
    }
    
    
    [Required] public IList<V1PolicyRule> Rules { get; set; }

    public bool NewUserDefault { get; set; } = false;

    [Required] public string DisplayName { get; set; }

    public string Description { get; set; }

    public bool Builtin { get; set; } = false;
}