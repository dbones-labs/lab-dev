namespace Dev.v1.Core;

using System.Text.RegularExpressions;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using Services;

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
    
    public static string TenancyLabel() => "lab.dev/tenancy";
}

public class TenancySpec
{
    
    /// <summary>
    /// this indicates that the team is a platform team
    /// </summary>
    [AdditionalPrinterColumn] public bool IsPlatform { get; set; } = false;
    
    /// <summary>
    /// this will be used to select which zones a tenancy has access to
    /// </summary>
    public List<Selector> ZoneFilter { get; set; } = new();

    public bool Where(Zone zone)
    {
        return !ZoneFilter.Any() || ZoneFilter.All(x => x.Where(zone));
    }
}

public class Selector
{
    [Required] public string Key { get; set; } = string.Empty;
    [Required] public Operator Operator { get; set; } = Operator.Equals;
    [Required] public string Value { get; set; } = string.Empty;

    public bool Where(Zone zone)
    {
        var filterForValue = Value;
        var hasLabel = zone.Metadata.Labels.TryGetValue(Key, out var value);
        if (!hasLabel || value == null) return false;

        return Operator switch
        {
            Operator.Pattern => Regex.IsMatch(value, filterForValue),
            Operator.StartsWith => value.StartsWith(filterForValue),
            Operator.Equals => value == filterForValue,
            Operator.NotEquals => value != filterForValue,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
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