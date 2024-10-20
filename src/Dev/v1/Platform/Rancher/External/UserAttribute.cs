namespace Dev.v1.Platform.Rancher.External;

using System.Text.Json.Serialization;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[IgnoreEntity]
[PartiallyMappedEntity]
[KubernetesEntity(Group = "management.cattle.io", ApiVersion = "v3")]
public class UserAttribute : CustomKubernetesEntity
{
    //[JsonPropertyName("ExtraByProvider")]
    //public Dictionary<string, RancherPrincipal?> extraByProvider { get; set; } = new();
    public Principals ExtraByProvider { get; set; }
}

public class Principals
{
    public RancherPrincipal? Github { get; set; }
}



public class RancherPrincipal
{
    public string Principalid { get; set; }
    public string Username { get; set; }
}
