namespace Dev.v1.RabbitMq;

using System.ComponentModel.DataAnnotations;
using KubeOps.Operator.Entities;

public class RabbitMq : CustomKubernetesEntity<RabbitMqSpec, RabbitMqStatus>
{
    
}

public class RabbitMqSpec
{
    [Required] public string Credentials { get; set; } = string.Empty;

    public bool UseSshTunnel { get; set; } = false;
}

public class RabbitMqStatus
{
    
}
