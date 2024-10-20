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
   // /// <summary>  
   // /// is this environment a production one
   // /// </summary>
   // [Required]
   // [AdditionalPrinterColumn] 
   // public bool IsProduction { get; set; }

   /// <summary>
   /// the type of environment this is
   /// </summary>
   [Required]
   [AdditionalPrinterColumn] 
   public EnvironmentType Type { get; set; } = EnvironmentType.PreProduction;
}

public enum EnvironmentType
{
   /// <summary>
   /// Live, Production zones, where the company hosts is systems
   /// </summary>
   Production,
   
   /// <summary>
   /// Pre production environments to ensure new features work as planned.
   /// </summary>
   PreProduction,
   
   /// <summary>
   /// local development, like a developer machine
   /// </summary>
   Engineering
}

public class EnvironmentStatus {}