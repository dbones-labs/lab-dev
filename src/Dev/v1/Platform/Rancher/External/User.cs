namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class User : CustomKubernetesEntity
{
    public List<string> PrincipalIds { get; set; } = new();
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}