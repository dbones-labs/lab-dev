namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// this denotes a tenancy within a zone
/// </summary>
/// <remarks>
/// this will most likely generated from the <see cref="Tenancy"/>
/// </remarks>
[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class TenancyInZone : CustomKubernetesEntity<TenancyInZoneSpec, TenancyInZoneStatus>
{
    public static string ZoneTenancyLabel() => "lab.dev/zoneTenancy";
}

public class TenancyInZoneSpec
{
    //[Required] public string? Zone { get; set; }

    [Required] public string Tenancy { get; set; } = string.Empty;
}


public class TenancyInZoneStatus
{
}