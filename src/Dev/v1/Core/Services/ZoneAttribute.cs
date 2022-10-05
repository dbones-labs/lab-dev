namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class ZoneAttribute : CustomKubernetesEntity<ZoneAttributeSpec, ZoneAttributeStatus>
{
    public static string EnvironmentLabel() => "lab.dev/environment";
    public static string RegionLabel() => "lab.dev/region";
    public static string CloudLabel() => "lab.dev/cloud";
    public static string ZoneLabel() => "lab.dev/zone";
    public static string EnvironmentTypeLabel() => "lab.dev/env-type";
}

public class ZoneAttributeSpec
{
}

public class ZoneAttributeStatus
{
}