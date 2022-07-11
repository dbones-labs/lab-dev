namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Organization : CustomKubernetesEntity<OrganizationSpec, OrganizationStatus> { }

public class OrganizationSpec
{
    /// <summary>
    /// how long to retain the service, when it have been deleted
    /// this allows for a service to be transferred between tenancies (default 1 week)
    /// </summary>
    public int RetainFor { get; set; } = 604800;

    public bool ProductionAccessWorkFlow { get; set; } = true;
    public string ProductionName { get; set; } = "production";
    

}

public class OrganizationStatus
{
}