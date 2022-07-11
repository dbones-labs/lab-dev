namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "internal.lab.dev", ApiVersion = "v1")]
public class TenancyContext : CustomKubernetesEntity<TenancyContextSpec, TenancyContextStatus>
{
    public static string GetName()
    {
        return "context";
    }
}

public class TenancyContextSpec
{
    public string OrganizationNamespace { get; set; }
}

public class TenancyContextStatus { }

