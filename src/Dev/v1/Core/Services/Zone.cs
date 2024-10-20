namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Zone : CustomKubernetesEntity<ZoneSpec, ZoneStatus>
{
    public static string EnvironmentLabel() => "lab.dev/environment";
    public static string RegionLabel() => "lab.dev/region";
    public static string CloudLabel() => "lab.dev/cloud";
    public static string ZoneLabel() => "lab.dev/zone";
    public static string EnvironmentTypeLabel() => "lab.dev/env-type";
}

public class ZoneSpec
{
    /// <summary>
    /// the name of the environment this belongs to
    /// </summary>
    [Required]
    [AdditionalPrinterColumn]
    public string Environment { get; set; } = "production";
    
    /// <summary>
    /// aws, azure, on-prem, scaleway, etc
    /// </summary>
    [Required]
    [AdditionalPrinterColumn]
    public string Cloud { get; set; } = string.Empty;
    
    /// <summary>
    /// where this zone is located
    /// </summary>
    [Required]
    [AdditionalPrinterColumn]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// denotes is this cluster is where rancher is running
    /// </summary>
    public bool IsControl { get; set; } = false;
}

public class ZoneStatus
{
    //public bool IsProduction { get; set; }

    public EnvironmentType Type { get; set; } = EnvironmentType.PreProduction;
}