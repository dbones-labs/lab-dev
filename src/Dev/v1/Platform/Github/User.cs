namespace Dev.v1.Platform.Github;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "github.internal.lab.dev", ApiVersion = "v1")]
public class User : CustomKubernetesEntity<UserSpec, UserStatus> { }

public class UserSpec
{
    /// <summary>
    /// the github username
    /// </summary>
    /// <remarks>
    /// the Account name is the MASTER for all main operations
    /// </remarks>
    [Required] public string Login { get; set; } = string.Empty;
}

public class UserStatus
{
    public string? GithubId { get; set; }
    public OrganisationStatus OrganisationStatus { get; set; } = OrganisationStatus.NotAMember;
}

public enum OrganisationStatus
{
    NotAMember,
    Invited,
    Member,
}