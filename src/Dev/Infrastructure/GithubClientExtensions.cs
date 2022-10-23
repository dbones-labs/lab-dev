namespace Dev.Controllers.Github.Internal;

using Octokit;

public static class GithubClientExtensions
{
    public static void Auth(this GitHubClient client, string token)
    {
        var tokenAuth = new Credentials(token);
        client.Credentials = tokenAuth;
    }
}