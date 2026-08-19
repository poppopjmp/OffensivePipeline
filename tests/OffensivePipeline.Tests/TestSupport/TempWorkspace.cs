using OffensivePipeline.Config;

namespace OffensivePipeline.Tests.TestSupport;

/// <summary>
/// A throwaway installation root, with the <see cref="PipelinePaths"/> that point at it.
/// </summary>
/// <remarks>
/// Every filesystem-touching test owns one of these. xunit v3 runs test collections in parallel, so
/// nothing may rely on <see cref="AppContext.BaseDirectory"/> or on any process-wide state; being
/// able to root the whole application at a temporary directory is exactly what replacing the former
/// static <c>Conf</c> class with an injected record bought.
/// </remarks>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(
            Path.GetTempPath(), "offensivepipeline-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(Root);
        Paths = new PipelinePaths(Root);
    }

    /// <summary>Absolute path of the temporary installation root.</summary>
    public string Root { get; }

    /// <summary>Paths rooted at <see cref="Root"/>.</summary>
    public PipelinePaths Paths { get; }

    /// <summary>Creates a directory beneath the root and returns its absolute path.</summary>
    public string Dir(params string[] segments)
    {
        string path = Path.Combine([Root, .. segments]);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Writes a file beneath the root, creating parent directories, and returns its path.</summary>
    public string File(string relativePath, string content)
    {
        string path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Copies the two shipped <c>Resources</c> templates into this workspace, so modules that render
    /// them run against the files that actually ship rather than against a test-local copy.
    /// </summary>
    public void CopyShippedResources()
    {
        Directory.CreateDirectory(Paths.ResourcesPath);
        string source = Path.Combine(AppContext.BaseDirectory, "Resources");
        foreach (string file in Directory.GetFiles(source))
        {
            System.IO.File.Copy(file, Path.Combine(Paths.ResourcesPath, Path.GetFileName(file)), true);
        }
    }

    /// <summary>
    /// Copies the shipped <c>appsettings.json</c> into this workspace, so the configuration the
    /// application is bound against in a test is the file that actually ships.
    /// </summary>
    public void CopyShippedAppSettings() => System.IO.File.Copy(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"), Paths.AppSettingsFile, true);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a green test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
