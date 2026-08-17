namespace OffensivePipeline.Config;

/// <summary>
/// Every well-known location the pipeline reads from or writes to, derived from a single root.
/// </summary>
/// <remarks>
/// Replaces the former static <c>Conf</c> class. Because the root is a constructor parameter rather
/// than a static field, a test can point the whole application at a temporary directory and run in
/// parallel with other tests. Every member is computed on demand, so <c>with</c> expressions stay
/// consistent.
/// </remarks>
/// <param name="BaseDirectory">Root the installation lives in.</param>
public sealed record PipelinePaths(string BaseDirectory)
{
    /// <summary>Paths for the running installation, rooted at the folder holding the executable.</summary>
    public static PipelinePaths ForCurrentInstallation() => new(AppContext.BaseDirectory);

    /// <summary>Where tool repositories are cloned or copied to.</summary>
    public string GitToolsPath => Path.Combine(BaseDirectory, "Git");

    /// <summary>Where per-run build output is written.</summary>
    public string OutputPath => Path.Combine(BaseDirectory, "Output");

    /// <summary>Folder holding the <c>*.yml</c> tool templates.</summary>
    public string YmlsPath => Path.Combine(BaseDirectory, "Tools");

    /// <summary>Diagnostic log written by the file logger provider.</summary>
    public string LogFile => Path.Combine(BaseDirectory, "log.txt");

    /// <summary>Folder holding downloaded third-party tooling and the batch templates.</summary>
    public string ResourcesPath => Path.Combine(BaseDirectory, "Resources");

    /// <summary>Downloaded <c>nuget.exe</c>, used to restore a tool's packages before building.</summary>
    public string NugetPath => Path.Combine(ResourcesPath, "nuget.exe");

    /// <summary>Template the generated <c>buildSolution.bat</c> is rendered from.</summary>
    public string TemplateBuildPath => Path.Combine(ResourcesPath, "template_build.bat");

    /// <summary>Folder the ConfuserEx CLI is extracted into.</summary>
    public string ConfuserExFolder => Path.Combine(ResourcesPath, "ConfuserEx");

    /// <summary>The ConfuserEx CLI executable.</summary>
    public string ConfuserExFile => Path.Combine(ConfuserExFolder, "Confuser.CLI.exe");

    /// <summary>Template each generated <c>.crproj</c> is rendered from.</summary>
    public string ConfuserTemplateFile =>
        Path.Combine(ResourcesPath, "template_confuserEx.crproj.template");

    /// <summary>The application settings file, and the legacy config it superseded.</summary>
    public string AppSettingsFile => Path.Combine(BaseDirectory, "appsettings.json");

    /// <inheritdoc cref="AppSettingsFile"/>
    public string LocalAppSettingsFile => Path.Combine(BaseDirectory, "appsettings.Local.json");

    /// <inheritdoc cref="AppSettingsFile"/>
    public string LegacyAppConfigFile => Path.Combine(BaseDirectory, "OffensivePipeline.dll.config");
}
