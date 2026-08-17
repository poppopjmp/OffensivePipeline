using LibGit2Sharp;

namespace OffensivePipeline.Infrastructure;

/// <summary>Clones repositories with LibGit2Sharp.</summary>
public sealed class GitClient : IGitClient
{
    public void Clone(string sourceUrl, string destinationPath, string? userName, string? accessToken)
    {
        var options = new CloneOptions { RecurseSubmodules = true };
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            string user = userName ?? string.Empty;
            string token = accessToken;
            options.FetchOptions.CredentialsProvider =
                (_, _, _) => new UsernamePasswordCredentials { Username = user, Password = token };
        }

        _ = Repository.Clone(sourceUrl, destinationPath, options);
    }
}
