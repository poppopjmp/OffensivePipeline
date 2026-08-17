namespace OffensivePipeline.Infrastructure;

/// <summary>
/// The thin wrapper over LibGit2Sharp. Everything that decides <em>whether</em> and <em>where</em>
/// to clone lives in <see cref="GitHelpers"/>; this interface is only the network call, so tests
/// can exercise the surrounding logic without reaching github.com.
/// </summary>
public interface IGitClient
{
    /// <summary>
    /// Clones <paramref name="sourceUrl"/> into <paramref name="destinationPath"/>, including
    /// submodules. Credentials are supplied only when <paramref name="accessToken"/> is set.
    /// </summary>
    /// <exception cref="Exception">Any clone failure is surfaced to the caller to report.</exception>
    void Clone(string sourceUrl, string destinationPath, string? userName, string? accessToken);
}
