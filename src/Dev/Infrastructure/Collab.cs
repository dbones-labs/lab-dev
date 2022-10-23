namespace Dev.Infrastructure;

[Obsolete("need to refactor", false)]
public static class Collab
{
    public static string GetCollabName(string repositoryName, string teamName)
    {
        return $"{repositoryName}-{teamName}";
    }

}