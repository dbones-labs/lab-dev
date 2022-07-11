namespace Dev.v1.Core;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;


[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
[EntityScope(EntityScope.Cluster)]
public class Member : CustomKubernetesEntity<MemberSpec,MemberStatus> { }

public class MemberSpec
{
    //[Required] public string? Tenancy { get; set; }

    [Required] public string Account { get; set; } = string.Empty;
    
    public MemberRole Role { get; set; } = MemberRole.Member;
}

public enum MemberRole
{
    /// <summary>
    /// only in stature
    /// </summary>
    Owner,
    
    /// <summary>
    /// a member that works in this tenancy
    /// </summary>
    Member,
    
    /// <summary>
    /// a guest that has limited access
    /// </summary>
    Guest
} 

public class MemberStatus
{
    
}