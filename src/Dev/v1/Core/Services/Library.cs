namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using Platform.Github;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Package : CustomKubernetesEntity<PackageSpec, PackageStatus>
{
}

public class PackageSpec
{
    public Visibility Visibility { get; set; } = Visibility.Internal;
}


public class PackageStatus { }