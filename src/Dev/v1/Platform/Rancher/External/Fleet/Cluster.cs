namespace Dev.v1.Platform.Rancher.External.Fleet;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// this is the rancher cluster, atm we are only interested in a small sub set
/// needs to be keep uptodate with Rancher <see cref="External.Cluster"/>
/// </summary>
[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "fleet.cattle.io", ApiVersion = "v1alpha1", PluralName = "clusters")]
public class Cluster : CustomKubernetesEntity<ClusterSpec, ClusterStatus>
{
    public static string NameLabel() => "management.cattle.io/cluster-display-name";
    public static string IdLabel() => "management.cattle.io/cluster-name";
    public static string Name() => "lab.dev/name";
}

public class ClusterSpec
{
}

public class ClusterStatus
{
}