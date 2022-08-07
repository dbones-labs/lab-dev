namespace Dev.v1.Core.Services;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Environment : CustomKubernetesEntity<EnvironmentSpec, EnvironmentStatus>
{
}

public class EnvironmentSpec
{
   /// <summary>  
   /// is this environment a production one
   /// </summary>
   [Required]
   [AdditionalPrinterColumn] 
   public bool IsProduction { get; set; }
}

public class EnvironmentStatus {}