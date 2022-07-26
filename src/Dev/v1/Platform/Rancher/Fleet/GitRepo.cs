namespace Dev.v1.Platform.Rancher.Fleet;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[KubernetesEntity(Group = "fleet.cattle.io", ApiVersion = "v1alpha1")]
public class GitRepo : CustomKubernetesEntity<GitRepoSpec, GitRepoStatus> { }


public class GitRepoSpec
{
    [Required] public string Branch { get; set; }
    [Required] public string ClientSecretName { get; set; }
    public bool InsecureSkipTLSVerify { get; set; } = false;
    [Required] public string Repo { get; set; }
}

public class ClusterSelector
{
    
}

public class MatchSelector
{
    public string Key { get; set; }
    public string Operator { get; set; }
}

public class GitRepoStatus {}