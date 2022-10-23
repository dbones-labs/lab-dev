namespace Dev.Infrastructure;

using Octokit;

public static class GithubClientExtensions
{
    public static void Auth(this GitHubClient client, string token)
    {
        var tokenAuth = new Credentials(token);
        client.Credentials = tokenAuth;
    }
}