namespace Dev.v1.Core.Services;

using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using Platform.Github;

public class Service : CustomKubernetesEntity<ServiceSpec, ServiceStatus>
{
    
}

public class ServiceSpec
{
    [AdditionalPrinterColumn] public Visibility Visibility { get; set; } = Visibility.Internal;
    public List<Zone> Zones { get; set; } = new();
}

public class ZoneEntry
{
    [Required] public string Name { get; set; } = string.Empty;
    public List<ComponentEntry> Components { get; set; } = new();
}

public class ComponentEntry
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Provider { get; set; } = string.Empty;
}

public class ServiceStatus
{
}

