namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Tenancy : CustomKubernetesEntity<TenancySpec, TenancyStatus>
{
    // public static string GetNamespaceName(string teamName)
    // {
    //     return $"tenancy-{teamName}";
    // }
    //
    // public static string GetBaseName(string teamName)
    // {
    //     return teamName.Replace("tenancy-", "");
    // }
}

public class TenancySpec
{
    public bool IsPlatform { get; set; } = false;
    public string ZoneFilter { get; set; } = String.Empty;
}

public class TenancyStatus 
{
}