namespace Dev.v1.Platform.Rancher.External;

using System.Collections.ObjectModel;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

/// <summary>
/// used to create and read projects.
/// </summary>
[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class Project : CustomKubernetesEntity<ProjectSpec, ProjectStatus>
{
    /// <summary>
    /// setup rancher project this what rancher requires pre-configured
    /// </summary>
    public static void Init(Project project, string name, string creatorId, string clusterId)
    {
        project.Metadata ??= new V1ObjectMeta();
        project.Metadata.Annotations ??= new Dictionary<string, string>();
        
        var a = project.Metadata.Annotations;
        
        a["authz.management.cattle.io/creator-role-bindings"] =
            "{\"created\":[\"project-owner\"],\"required\":[\"project-owner\"]}";
        a["field.cattle.io/creatorId"] = creatorId;
        a["lifecycle.cattle.io/create.mgmt-project-rbac-remove"] = "true";
        a[$"lifecycle.cattle.io/create.project-namespace-auth_{clusterId}"] = "true";

        project.Metadata.Finalizers ??= new List<string>();
        var f = project.Metadata.Finalizers;
        f.Add("controller.cattle.io/mgmt-project-rbac-remove");
        f.Add($"clusterscoped.controller.cattle.io/project-namespace-auth_{clusterId}");

        project.Metadata.GenerateName = "p-";
        project.Metadata.NamespaceProperty = clusterId;

        project.Spec.ClusterName = clusterId;
        project.Spec.DisplayName = name;
    }
}

public class ProjectSpec
{
    [Required] public string? ClusterName { get; set; }
    [Required] public string DisplayName { get; set; }
    //public bool EnableProjectMonitoring { get; set; } = false;
}

public class ProjectStatus
{
}