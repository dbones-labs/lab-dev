namespace Dev.v1.Platform.Rancher.External.Fleet;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "fleet.cattle.io", ApiVersion = "v1alpha1")]
public class ClusterGroup : CustomKubernetesEntity<ClusterGroupSpec, ClusterGroupStatus> { }


public class ClusterGroupSpec
{
    public GroupSelector Selector { get; set; } = new();
}

public class GroupSelector
{
    public List<MatchSelector>? MatchExpressions { get; set; }
}

public class MatchSelector
{
    public string Key { get; set; }
    public string Operator { get; set; }
    public List<string>? Values { get; set; }
}

public class ClusterGroupStatus {}