namespace Dev.v1.Platform.Rancher;

using System.ComponentModel.DataAnnotations;
using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Rancher : CustomKubernetesEntity<RancherSpec,  RancherStatus> { }

public class RancherSpec
{
    [Required] public string? Url { get; set; }
    [Required] public string? Credentials { get; set; }
}

public class RancherStatus
{
    public V1SecretReference? CredentialsReference { get; set; } 
}