namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

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
    
    /// <summary>
    /// this indicates that the team is a platform team
    /// </summary>
    public bool IsPlatform { get; set; } = false;
    
    /// <summary>
    /// this will be used to select which zones a tenancy has access to
    /// </summary>
    public List<Selector> ZoneFilter { get; set; } = new();
}

public class Selector
{
    [Required] public string Key { get; set; } = string.Empty;
    [Required] public Operator Operator { get; set; } = Operator.Equals;
    [Required] public string Value { get; set; } = string.Empty;
}

public enum Operator
{
    Pattern,
    StartsWith,
    Equals,
    NotEquals
}

public class TenancyStatus 
{
}