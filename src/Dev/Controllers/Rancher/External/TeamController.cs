namespace Dev.Controllers.Rancher.External;

using Infrastructure;
using Infrastructure.Caching;
using v1.Platform.Github;

public class TeamController : ResourceController<Team>
{
    public TeamController(ResourceCache cache) : base(cache)
    {
    }
    
    
}