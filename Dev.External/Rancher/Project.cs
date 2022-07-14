namespace Dev.External.Rancher;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "cattle.test", ApiVersion = "v3")]
public class Project : CustomKubernetesEntity<ProjectSpec, ProjectStatus>
{
}

public class ProjectSpec
{
    [Required] public string ClusterName { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public string DisplayName { get; set; } = string.Empty; 
    public bool EnableProjectMonitoring { get; set; } = false;
}

public class ProjectStatus
{
}