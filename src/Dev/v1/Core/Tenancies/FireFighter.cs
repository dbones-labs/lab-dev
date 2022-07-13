namespace Dev.v1.Core;

using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class FireFighter : CustomKubernetesEntity<FireFighterSpec, FireFighterStatus>
{
    public static string Requested() => "firefighter requested";
    public static string Approved() => "firefighter approved";
    public static string Activated() => "firefighter activated";
    public static string Completed() => "firefighter completed";
}

public class FireFighterSpec
{
    [Required] public string Account { get; set; } = String.Empty;
    [Required] public string Tenancy { get; set; } = String.Empty;
    [Required] public DateTime Start { get; set; }
    [Required] public DateTime Finish { get; set; }
}

public class FireFighterStatus
{
}