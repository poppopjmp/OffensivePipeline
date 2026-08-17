using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Infrastructure;

/// <summary>
/// Downloads over HTTPS and checks the result against a pinned SHA-256 before letting the pipeline
/// use it.
/// </summary>
/// <remarks>
/// Transport security alone says only that the bytes came from whoever holds the certificate for
/// that host today; it says nothing about the release having been replaced. A file that fails the
/// check is deleted rather than left on disk, so a later run cannot pick it up and skip the
/// download entirely.
/// </remarks>
public sealed class ResourceDownloader(ILogger<ResourceDownloader> logger, IConsoleUi ui)
    : IResourceDownloader, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public bool Download(string url, string outputName, string outputPath, string? expectedSha256)
    {
        string destination = Path.Combine(outputPath, outputName);
        try
        {
            Directory.CreateDirectory(outputPath);

            using (HttpResponseMessage response = _httpClient.GetAsync(url).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                using var fileStream = new FileStream(
                    destination, FileMode.Create, FileAccess.Write, FileShare.None);
                response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
            }

            if (!File.Exists(destination))
            {
                ui.Failure($"DownloadResources: File not found <{destination}>");
                logger.Error($"DownloadResources: no file produced at {destination}");
                return false;
            }

            return VerifyHash(destination, outputName, expectedSha256);
        }
        catch (Exception ex)
        {
            ui.Failure($"DownloadResources: <{url}> - {ex}");
            logger.Error(ex, $"DownloadResources failed: {url}");
            return false;
        }
    }

    private bool VerifyHash(string destination, string outputName, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            string warning = $"{outputName} was downloaded without integrity verification "
                + "(no expected SHA-256 configured); set it in appsettings.json to pin this download";
            ui.Warning(warning);
            logger.Warn(warning);
            return true;
        }

        string expected = expectedSha256.Trim();
        string actual = Helpers.ComputeSha256File(destination);
        if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            logger.Info($"Verified SHA-256 of {outputName}: {actual}");
            return true;
        }

        string message = $"DownloadResources: SHA-256 mismatch for {outputName} - "
            + $"expected {expected}, got {actual}";
        ui.Failure(message);
        logger.Error(message);

        TryDelete(destination);
        return false;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Could not delete unverified download {path}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
