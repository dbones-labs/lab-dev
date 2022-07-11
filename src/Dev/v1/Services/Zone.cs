namespace Dev.v1.Core;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
[EntityScope(EntityScope.Cluster)]
public class Zone : CustomKubernetesEntity<ZoneSpec, ZoneStatus> { }

public class ZoneSpec
{
    [Required]
    public string? Environment { get; set; }
}

public class ZoneStatus
{
    public bool IsProduction { get; set; }
}