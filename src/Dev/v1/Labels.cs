namespace Dev.v1;

public static class Labels
{
    public static string Environment = "lab.dev/environment";
    public static string Region = "lab.dev/region";
    public static string Cloud = "lab.dev/cloud";
    public static string Zone = "lab.dev/zone";
    public static string PlatformTeam = "lab.dev/platformTeam";
}


/// <summary>
/// This entity is only partially mapped, so any updates must be via
/// patch statements.
/// </summary>
public class PartiallyMappedEntityAttribute : Attribute
{
}