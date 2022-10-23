namespace Dev.Infrastructure;

using Octokit;

public static class HttpAssist
{
    public static async Task<T?> Get<T>(Func<Task<T>> call) where T : class
    {
        try
        {
            var result = await call.Invoke();
            return result;
        }
        catch (NotFoundException notFound)
        {
            return null;
        }
        
    }
}