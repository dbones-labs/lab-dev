namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Zone : CustomKubernetesEntity<ZoneSpec, ZoneStatus>
{
}

public class ZoneSpec
{
    /// <summary>
    /// the name of the environment this belongs to
    /// </summary>
    [Required] public string? Environment { get; set; }
    
    /// <summary>
    /// aws, azure, on-prem, scaleway, etc
    /// </summary>
    [Required] public string? Cloud { get; set; }
    
    /// <summary>
    /// where this zone is located
    /// </summary>
    [Required] public string? Region { get; set; }
}

public class ZoneStatus
{
    public bool IsProduction { get; set; }
}