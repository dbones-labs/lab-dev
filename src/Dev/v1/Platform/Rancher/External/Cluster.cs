﻿namespace Dev.v1.Platform.Rancher.External;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// this is the rancher cluster, atm we are only interested in a small sub set
/// needs to be keep uptodate with Fleet <see cref="Fleet.Cluster"/>
/// </summary>
[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3", PluralName = "clusters")]
public class Cluster : CustomKubernetesEntity<ClusterSpec, ClusterStatus>
{
    public static string NameLabel() => "management.cattle.io/cluster-display-name";
}

public class ClusterSpec
{
    public string DisplayName { get; set; }
    public string FleetWorkspaceName { get; set; }
    public bool Internal { get; set; }
}

public class ClusterStatus
{
}