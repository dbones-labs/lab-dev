namespace Dev.v1.Components.Kubernetes;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Kubernetes : CustomKubernetesEntity<KubernetesSpec, KubernetesStatus>
{
}

public class KubernetesSpec
{
    public bool IsPrimary { get; set; } = false;
}

public class KubernetesStatus { }