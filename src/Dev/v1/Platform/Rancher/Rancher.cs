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

    #region global
    /// <summary>
    /// the default role to set everyone to, by default user-base is seen to be more secure
    /// </summary>
    public string GlobalDefaultRole { get; set; } = "user-base";
    
    /// <summary>
    /// 
    /// </summary>
    public string GlobalOrganizationRole { get; set; } = "lab-view-fleet, view-rancher-metrics";
    #endregion
    
    
    #region zone
    //Zone Global (platform team)
    public string GlobalZoneMemberRole  { get; set; } = "lab-view-users";
    public string GlobalZoneFireFighterMemberRole { get; set; } = "admin";
    
    //Zone Cluster (zones will update all clusters)
    public string ClusterZoneMemberRole { get; set; } = "cluster-owner";
    public string ClusterProductionZoneMemberRole { get; set; } = "clusterroletemplatebindings-view, nodes-view, projects-view, clustercatalogs-view";
    public string ClusterZoneFireFighterMemberRole { get; set; } = "cluster-owner";
    #endregion
    
    
    #region tenancy
    //Tenancy

    //Tenancy Cluster
    public string ClusterTenancyMemberRole { get; set; } = "nodes-view";
    public string ClusterProductionTenancyMemberRole { get; set; } = "nodes-view";
    
    //Tenancy Project
    public string TenancyMemberRole { get; set; } = "project-member";
    public string TenancyProductionMemberRole { get; set; } = "read-only";
    public string TenancyFireFighterMemberRole { get; set; } = "project-member";
    #endregion


    #region review
    //Guest Roles (under review)
    public string TenancyGuestRole { get; set; } = "read-only";
    
    
    //plaform roles (to remove)
    public string PlatformMemberRole { get; set; } = "project-member";
    public string PlatformProductionMemberRole { get; set; } = "read-only";
    #endregion
    

    
}

public class RancherStatus { }