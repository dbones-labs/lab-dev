namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[Obsolete("being removed, we can access the org ns directly now", false)]
[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class TenancyContext : CustomKubernetesEntity<TenancyContextSpec, TenancyContextStatus>
{
    public static string GetName() => "context";
}

public class TenancyContextSpec
{
    [Required] public string OrganizationNamespace { get; set; } = string.Empty;
}

public class TenancyContextStatus { }

