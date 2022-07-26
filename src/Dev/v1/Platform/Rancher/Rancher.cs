namespace Dev.v1.Platform.Rancher;

using System.ComponentModel.DataAnnotations;
using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Rancher : CustomKubernetesEntity<RancherSpec,  RancherStatus> { }

public class RancherSpec
{
    /// <summary>
    /// this user will be assigned as the owner in a number of cases.
    /// should be a GOD user.
    /// </summary>
    [Required] public string TechnicalUser { get; set; } = "";
    
    //plaform roles (to confirm)
    
    public string PlatformMemberRole { get; set; } = "project-member";
    public string PlatformProductionMemberRole { get; set; } = "read-only";
    
    //tenancy roles
    
    public string TenancyGuestRole { get; set; } = "read-only";
    public string TenancyMemberRole { get; set; } = "project-member";
    public string TenancyProductionMemberRole { get; set; } = "read-only";
    public string TenancyFireFighterMemberRole { get; set; } = "project-member";

}

public class RancherStatus { }