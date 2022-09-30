namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class UserAttribute : CustomKubernetesEntity
{
    public Dictionary<string, RancherPrincipal> ExtraByProvider { get; set; }
}


public class RancherPrincipal
{
    public string Principalid { get; set; }
    public string Username { get; set; }
}
