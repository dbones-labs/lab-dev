namespace Dev.Infrastructure.Git;

using Dev.v1.Platform.Github;

internal record GitContext(
    Github Github, 
    Repository Repository, 
    string RepositoryFolder, 
    string Token, 
    Action Unlock);