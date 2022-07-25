namespace Dev.v1.Components.Kubernetes;

using k8s.Models;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Kubernetes
{
}

public class KubernetesSpec
{
    public bool IsPrimary { get; set; } = false;
}

public class KubernetesStatus { }