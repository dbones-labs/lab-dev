namespace Dev.v1.Core.Services;

using System.ComponentModel.DataAnnotations;
using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Environment : CustomKubernetesEntity<EnvironmentSpec, EnvironmentStatus>
{
}

public class EnvironmentSpec
{
   /// <summary>  
   /// is this environment a production one
   /// </summary>
   [Required] public bool IsProduction { get; set; }
}

public class EnvironmentStatus {}