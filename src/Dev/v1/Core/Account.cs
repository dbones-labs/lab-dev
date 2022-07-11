namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Account : CustomKubernetesEntity<AccountSpec, AccountStatus> { }

public class AccountSpec
{
    public List<ExternalAccount> ExternalAccounts { get; set; } = new List<ExternalAccount>();
}

public class ExternalAccount
{
    /// <summary>
    /// the provider which the account is to be associated with
    /// github, discord, rancher etc
    /// </summary>
    [Required] public string Provider { get; set; } = String.Empty;
    
    /// <summary>
    /// the account id of the external system
    /// </summary>
    [Required] public string Id { get; set; } = string.Empty;
}


public class AccountStatus
{
    public string? RancherId { get; set; }
}

