namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "github.internal.lab.dev", ApiVersion = "v1")]
public class Repository : CustomKubernetesEntity<RepositorySpec, RepositoryStatus>
{
    public static string RepositoryLabel() => "github.lab.dev/repository";
    public static string TypeLabel() => "github.lab.dev/type";
    public static string OwnerLabel() => "github.lab.dev/owner";
}

public class RepositorySpec
{
    /// <summary>
    /// control who can see the repository
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Private;
    
    /// <summary>
    /// is the repository is active or archived
    /// </summary>
    public State State { get; set; } = State.Active;
    
    /// <summary>
    /// the repo can be for projects/service (normal) or to adminsitrate/control (system)
    /// [default: Normal]
    /// </summary>
    public Type Type { get; set; } = Type.Normal;

    /// <summary>
    /// ensure only the managed collaborators are provided access.
    /// [default: false]
    /// </summary>
    public bool EnforceCollaborators { get; set; } = false;
    
    
    [Required] public string OrganizationNamespace { get; set; } = string.Empty;
}

public class RepositoryStatus
{
    public long? Id { get; set; }
}