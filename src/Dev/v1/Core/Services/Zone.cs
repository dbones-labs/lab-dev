namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Zone : CustomKubernetesEntity<ZoneSpec, ZoneStatus> { }

public class ZoneSpec
{
    [Required] public string? Environment { get; set; }
}

public class ZoneStatus
{
    public bool IsProduction { get; set; }
}