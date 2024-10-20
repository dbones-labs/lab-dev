namespace Dev.v1.Platform.Rancher.External.Fleet;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "fleet.cattle.io", ApiVersion = "v1alpha1")]
public class GitRepo : CustomKubernetesEntity<GitRepoSpec, GitRepoStatus> { }


public class GitRepoSpec
{
    [Required] public string Repo { get; set; } = string.Empty;
    [Required] public string Branch { get; set; } = string.Empty;
    [Required] public string ClientSecretName { get; set; } = string.Empty;
    public bool InsecureSkipTLSVerify { get; set; } = false;
    public string? ServiceAccount { get; set; }
    public List<string> Paths { get; set; } = new();
    public List<ClusterSelector> Targets { get; set; } = new();
    public string? TargetNamespace { get; set; }
}

public class ClusterSelector
{
    public string? ClusterGroup { get; set; }
    public List<MatchSelector>? MatchExpressions { get; set; }
}


public class GitRepoStatus {}