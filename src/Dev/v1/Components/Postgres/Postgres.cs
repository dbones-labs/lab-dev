namespace Dev.v1.Components.Postgres;

using System.ComponentModel.DataAnnotations;
using k8s.Models;
using KubeOps.Operator.Entities;

[KubernetesEntity(Group = "lab.dev", ApiVersion = "v1")]
public class Postgres : CustomKubernetesEntity<PostgresSpec, PostgresStatus>
{
    
}

public class PostgresSpec
{
    [Required] public string Credentials { get; set; } = string.Empty;

    public bool UseSshTunnel { get; set; } = false;
}

public class PostgresStatus { }
