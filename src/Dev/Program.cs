using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKubernetesOperator();
//    .AddResourceAssembly(typeof(Program).Assembly);

builder.Services.AddTransient<GitHubClient>(_ => new GitHubClient(new ProductHeaderValue("lab.dev")));
builder.Services.AddTransient<IGitHubClient>(provider => provider.GetRequiredService<GitHubClient>());

var app = builder.Build();
app.UseKubernetesOperator();

app.RunOperatorAsync(args).Wait();