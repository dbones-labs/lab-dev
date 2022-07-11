namespace Dev.Controllers.Github;

using v1.Platform.Github;

public static class Collab
{
    public static string GetCollabName(string repositoryName, string teamName)
    {
        return $"{repositoryName}-{teamName}";
    }

    public static Collaborator Create(string repositoryName, string teamName, string organizationNamespace, Membership membership)
    {
        var name = GetCollabName(repositoryName, teamName);
        return new Collaborator()
        {
            Metadata = new()
            {
                Name = name,
                NamespaceProperty = organizationNamespace
            },

            Spec = new()
            {
                Repository = repositoryName,
                Team = teamName,
                OrganizationNamespace = organizationNamespace,
                Membership = membership
            }
        };
    }
}