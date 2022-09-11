namespace Dev.v1.Core.Tenancies;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using Microsoft.AspNetCore.Authentication;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class FireFighter : CustomKubernetesEntity<FireFighterSpec, FireFighterStatus>
{
    public static string Requested() => "firefighter requested";
    public static string Approved() => "firefighter approved";
    public static string Activated() => "firefighter activated";
    
    public static string FireFighterLabel() => "lab.dev/fire-fighter";
    //public static string Completed() => "firefighter completed";
}

public class FireFighterSpec
{
    [Required] public string Account { get; set; } = String.Empty;
    
    [Required]
    [AdditionalPrinterColumn] 
    public string Tenancy { get; set; } = String.Empty;
    
    [Required] public DateTime Start { get; set; }
    
    [Required]
    [AdditionalPrinterColumn] 
    public DateTime Finish { get; set; }
    public int Number { get; set; }
    public long RepositoryId { get; set; }
}

public class FireFighterStatus
{
    public State State { get; set; }
}

public enum State
{
    Requested,
    Approved,
    Activated,
    Completed
}