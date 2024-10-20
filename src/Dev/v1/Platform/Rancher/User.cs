namespace Dev.v1.Platform.Rancher;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// this is the association of the Account, to allow us to Reference the Rancher user if and when needed.
/// </summary>
[KubernetesEntity(Group = "rancher.internal.lab.dev", ApiVersion = "v1")]
public class User: CustomKubernetesEntity<UserSpec, UserStatus>
{
    public static string AccountLabel() => "lab.dev/account"; 
    public static string LoginLabel() => "rancher.lab.dev/login";
}

public class UserSpec
{
    /// <summary>
    /// the Rancher username
    /// </summary>
    /// <remarks>
    /// the Account name is the MASTER for all main operations
    /// </remarks>
    [Required] public string Login { get; set; } = string.Empty;
}

public class UserStatus
{
}

