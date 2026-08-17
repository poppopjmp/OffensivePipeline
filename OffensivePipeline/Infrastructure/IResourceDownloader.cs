namespace OffensivePipeline.Infrastructure;

/// <summary>
/// Fetches a third-party binary the pipeline needs and verifies it before it is ever executed.
/// </summary>
public interface IResourceDownloader
{
    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="outputPath"/>/<paramref name="outputName"/>.
    /// </summary>
    /// <param name="expectedSha256">
    /// Hex SHA-256 the download must match. When null or empty the check is skipped and a warning
    /// is reported: <c>nuget.exe</c> and the ConfuserEx CLI are executed against client networks,
    /// so running them unverified is a decision the operator should see being made.
    /// </param>
    /// <returns>True only when the file arrived and matched the expected hash.</returns>
    bool Download(string url, string outputName, string outputPath, string? expectedSha256);
}
