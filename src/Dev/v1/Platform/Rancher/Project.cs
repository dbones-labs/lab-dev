namespace Dev.v1.Platform.Rancher;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "internal.lab.dev", ApiVersion = "v1")]
public class Project : CustomKubernetesEntity<ProjectSpec, ProjectSpec>
{
    
}

public class ProjectSpec
{
    [Required] public string? Name { get; set; }
    [Required] public string? Cluster { get; set; }
    [Required] public string? OwnedBy { get; set; }
}

public class ProjectStatus
{
    public string? Id { get; set; }
}