namespace Dev.Controllers.Postgresql;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using v1.Core;

[EntityRbac(typeof(Account), Verbs = RbacVerb.All)]
public class AccountController : IResourceController<Account>
{
    private readonly ILogger<AccountController> _logger;

    public AccountController(ILogger<AccountController> logger)
    {
        _logger = logger;
    }
    
    public Task<ResourceControllerResult?> ReconcileAsync(Account? entity)
    {
        if (entity == null) 
        {
            return Task.FromResult((ResourceControllerResult?)null);
        }
        _logger.LogInformation("postgres account controller {Name}", entity.Metadata.Name);
        return Task.FromResult((ResourceControllerResult?)null);
    }
}