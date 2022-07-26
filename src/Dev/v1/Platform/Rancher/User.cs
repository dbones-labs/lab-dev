namespace Dev.v1.Platform.Rancher;

using k8s.Models;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class User
{
    public List<string> PrincipalIds { get; set; } = new();
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}

public class UserSpec { }

public class UserStatus { }