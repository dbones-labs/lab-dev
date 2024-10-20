namespace Dev.v1.Platform.Rancher;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;


/// <summary>
/// used to create and read projects.
/// </summary>
[KubernetesEntity(Group = "rancher.internal.lab.dev", ApiVersion = "v1")]
public class Project : CustomKubernetesEntity<ProjectSpec, ProjectStatus>
{
    public static string ProjectLabel() => "lab.dev/k8s-project";
}

public class ProjectSpec
{
    /// <summary>
    /// the name of the tenancy (not the name of this resource)
    /// </summary>
    [Required] public string Tenancy { get; set; } = string.Empty;
    /// <summary>
    /// the cluster this belongs too
    /// </summary>
    [Required] public string Kubernetes { get; set; } = string.Empty;
}

public class ProjectStatus
{
    /// <summary>
    /// the rancher project id (as it get generated, and placed into another namespace)
    /// </summary>
    public string? Id { get; set; }
}