using OffensivePipeline.Config;

namespace OffensivePipeline.Tests;

/// <summary>
/// The path record that replaced the former static <c>Conf</c>. Everything derives from one root,
/// which is what lets a test run the whole application inside a temporary directory.
/// </summary>
public class PipelinePathsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "op-root");

    private static readonly PipelinePaths Paths = new(Root);

    [Fact]
    public void Top_Level_Folders_Hang_Off_The_Base_Directory()
    {
        Assert.Equal(Path.Combine(Root, "Git"), Paths.GitToolsPath);
        Assert.Equal(Path.Combine(Root, "Output"), Paths.OutputPath);
        Assert.Equal(Path.Combine(Root, "Tools"), Paths.YmlsPath);
        Assert.Equal(Path.Combine(Root, "Resources"), Paths.ResourcesPath);
        Assert.Equal(Path.Combine(Root, "log.txt"), Paths.LogFile);
    }

    [Fact]
    public void Resource_Paths_Hang_Off_The_Resources_Folder()
    {
        Assert.Equal(Path.Combine(Paths.ResourcesPath, "nuget.exe"), Paths.NugetPath);
        Assert.Equal(Path.Combine(Paths.ResourcesPath, "template_build.bat"), Paths.TemplateBuildPath);
        Assert.Equal(Path.Combine(Paths.ResourcesPath, "ConfuserEx"), Paths.ConfuserExFolder);
        Assert.Equal(
            Path.Combine(Paths.ConfuserExFolder, "Confuser.CLI.exe"), Paths.ConfuserExFile);
        Assert.Equal(
            Path.Combine(Paths.ResourcesPath, "template_confuserEx.crproj.template"),
            Paths.ConfuserTemplateFile);
    }

    [Fact]
    public void Configuration_File_Paths_Sit_Beside_The_Executable()
    {
        Assert.Equal(Path.Combine(Root, "appsettings.json"), Paths.AppSettingsFile);
        Assert.Equal(Path.Combine(Root, "appsettings.Local.json"), Paths.LocalAppSettingsFile);
        Assert.Equal(Path.Combine(Root, "OffensivePipeline.dll.config"), Paths.LegacyAppConfigFile);
    }

    /// <summary>
    /// Members are computed rather than captured, so re-rooting with <c>with</c> stays consistent
    /// instead of leaving half the paths pointing at the old installation.
    /// </summary>
    [Fact]
    public void Rerooting_With_A_With_Expression_Moves_Every_Derived_Path()
    {
        string other = Path.Combine(Path.GetTempPath(), "op-other");
        PipelinePaths moved = Paths with { BaseDirectory = other };

        Assert.Equal(Path.Combine(other, "Git"), moved.GitToolsPath);
        Assert.Equal(Path.Combine(other, "Resources", "nuget.exe"), moved.NugetPath);
        Assert.Equal(
            Path.Combine(other, "Resources", "ConfuserEx", "Confuser.CLI.exe"), moved.ConfuserExFile);
    }

    [Fact]
    public void The_Running_Installation_Is_Rooted_At_The_Executables_Folder() =>
        Assert.Equal(AppContext.BaseDirectory, PipelinePaths.ForCurrentInstallation().BaseDirectory);
}
