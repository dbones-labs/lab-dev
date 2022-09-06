using Dev.Controllers.Rancher.Git;
using KubeOps.Operator;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKubernetesOperator();
//    .AddResourceAssembly(typeof(Program).Assembly);

builder.Services.AddTransient<GitHubClient>(_ => new GitHubClient(new ProductHeaderValue("lab.dev")));
builder.Services.AddTransient<IGitHubClient>(provider => provider.GetRequiredService<GitHubClient>());
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<Templating>();

var app = builder.Build();
app.UseKubernetesOperator();

app.RunOperatorAsync(args).Wait();