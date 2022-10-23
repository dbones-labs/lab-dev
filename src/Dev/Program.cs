using Dev.Controllers.Rancher.Git;
using Dev.Infrastructure.Caching;
using Dev.Infrastructure.Git;
using Dev.Infrastructure.Templates;
using DotnetKubernetesClient;
using k8s;
using KubeOps.Operator;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKubernetesOperator();
//    .AddResourceAssembly(typeof(Program).Assembly);

builder.Services.AddSingleton<IKubernetesClient, KubernetesClient>((svc)=>
{
    var config = KubernetesClientConfiguration.BuildDefaultConfig();
    config.SkipTlsVerify = true;

    var client = new KubernetesClient(config);
    return client;
});


builder.Services.AddTransient<GitHubClient>(_ => new GitHubClient(new ProductHeaderValue("lab.dev")));
builder.Services.AddTransient<IGitHubClient>(provider => provider.GetRequiredService<GitHubClient>());
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<Templating>();
builder.Services.AddSingleton<ICache, InMemoryCache>();
builder.Services.AddSingleton<ResourceCache>();

var app = builder.Build();
app.UseKubernetesOperator();

app.RunOperatorAsync(args).Wait();