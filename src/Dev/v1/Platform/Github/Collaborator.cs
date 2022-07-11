namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// teams which collab on a repo (in our case its a TEAM having access)
/// </summary>
[KubernetesEntity(Group = "github.internal.lab.dev", ApiVersion = "v1")]
public class Collaborator : CustomKubernetesEntity<CollaboratorSpec, CollaboratorStatus> { }

public class CollaboratorSpec
{
    [Required] public string Team { get; set; } = string.Empty;
    [Required] public string Repository { get; set; } = string.Empty;
    public Membership Membership { get; set; } = Membership.Pull;

    [Required] public string OrganizationNamespace { get; set; } = string.Empty;
}

public class CollaboratorStatus {}


public enum Membership
{
    Pull,
    Push,
    Admin
}