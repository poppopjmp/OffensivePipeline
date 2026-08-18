using System.IO.Compression;
using OffensivePipeline.Config;
using OffensivePipeline.Modules;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The obfuscation module, exercised through the process-runner seam so ConfuserEx is never run.
/// </summary>
public class ConfuserExTests
{
    private static PipelineOptions Options() => new()
    {
        NugetUrl = "https://example.invalid/nuget.exe",
        BuildCsharpOptions = "/p:Configuration=Release",
        BuildCSharpTools = "/vs.bat",
        ConfuserExUrl = "https://example.invalid/ConfuserEx-CLI.zip",
        ConfuserExSha256 = "ab".PadRight(64, '0'),
    };

    private static Modules.ConfuserEx Module(
        TempWorkspace workspace,
        RecordingConsoleUi ui,
        RecordingProcessRunner runner,
        FakeResourceDownloader downloader) =>
        new(ui, TestFactory.Log<Modules.ConfuserEx>(), workspace.Paths, Options(), runner, downloader);

    /// <summary>A workspace where the ConfuserEx CLI is already present, so nothing is downloaded.</summary>
    private static ModuleContext Arrange(TempWorkspace workspace, out string previousFolder)
    {
        workspace.CopyShippedResources();
        Directory.CreateDirectory(workspace.Paths.ConfuserExFolder);
        File.WriteAllText(workspace.Paths.ConfuserExFile, "confuser");

        previousFolder = workspace.Dir("Output", "MyTool_abc123");
        File.WriteAllText(Path.Combine(previousFolder, "payload.exe"), "exe bytes");
        File.WriteAllText(Path.Combine(previousFolder, "Vendor.dll"), "dll bytes");

        return new ModuleContext(
            TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/MyTool.sln"),
            previousFolder,
            workspace.Paths.GitToolsPath);
    }

    [Fact]
    public void CheckStart_Creates_The_Nested_Output_Folder_And_Reports_It()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);

        ModuleResult result = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader())
            .CheckStart(context);

        string expected = Path.Combine(previousFolder, "ConfuserEx");
        Assert.True(result.Status);
        Assert.Equal(expected, result.OutputPath);
        Assert.True(Directory.Exists(expected));
    }

    /// <summary>
    /// The project file used to be written next to the built executable, because the rooted exe
    /// path made <c>Path.Combine</c> discard the output folder entirely.
    /// </summary>
    [Fact]
    public void The_Crproj_Is_Written_To_The_Output_Folder_Not_Beside_The_Executable()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);
        Modules.ConfuserEx module = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader());
        module.CheckStart(context);

        module.Run(context);

        string outputFolder = Path.Combine(previousFolder, "ConfuserEx");
        Assert.True(File.Exists(Path.Combine(outputFolder, "payload.exe.crproj")));
        Assert.False(File.Exists(Path.Combine(previousFolder, "payload.exe.crproj")));
    }

    [Fact]
    public void The_Crproj_Has_Every_Placeholder_Substituted()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);
        Modules.ConfuserEx module = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader());
        module.CheckStart(context);

        module.Run(context);

        string outputFolder = Path.Combine(previousFolder, "ConfuserEx");
        string crproj = File.ReadAllText(Path.Combine(outputFolder, "payload.exe.crproj"));

        Assert.DoesNotContain("{{", crproj, StringComparison.Ordinal);
        Assert.Contains(previousFolder, crproj, StringComparison.Ordinal);
        Assert.Contains(outputFolder, crproj, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(previousFolder, "payload.exe"), crproj, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Cli_Is_Invoked_Once_Per_Executable_With_Its_Project_File()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);
        File.WriteAllText(Path.Combine(previousFolder, "second.exe"), "exe bytes");

        var runner = new RecordingProcessRunner();
        Modules.ConfuserEx module = Module(workspace, new RecordingConsoleUi(), runner, new FakeResourceDownloader());
        module.CheckStart(context);

        module.Run(context);

        string outputFolder = Path.Combine(previousFolder, "ConfuserEx");
        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains(
            $"{workspace.Paths.ConfuserExFile} {Path.Combine(outputFolder, "payload.exe.crproj")}",
            runner.Commands);
        Assert.Contains(
            $"{workspace.Paths.ConfuserExFile} {Path.Combine(outputFolder, "second.exe.crproj")}",
            runner.Commands);
    }

    [Fact]
    public void Companion_Assemblies_Are_Carried_Into_The_Output_Folder()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);
        Modules.ConfuserEx module = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader());
        module.CheckStart(context);

        ModuleResult result = module.Run(context);

        Assert.True(result.Status);
        Assert.Equal(
            "dll bytes",
            File.ReadAllText(Path.Combine(previousFolder, "ConfuserEx", "Vendor.dll")));
    }

    /// <summary>
    /// A non-zero exit from ConfuserEx used to print "[+] No errors!" regardless, because the
    /// command runner always reported success.
    /// </summary>
    [Fact]
    public void A_Failed_Obfuscation_Fails_The_Module()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out _);
        var ui = new RecordingConsoleUi();
        Modules.ConfuserEx module = Module(workspace, ui, new RecordingProcessRunner(1), new FakeResourceDownloader());
        module.CheckStart(context);

        ModuleResult result = module.Run(context);

        Assert.False(result.Status);
        Assert.Contains("Confusing: ", ui.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("No errors!", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckStart_Downloads_And_Extracts_The_Cli_When_It_Is_Absent()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        var downloader = new FakeResourceDownloader
        {
            OnDownload = destination => WriteZipContaining(destination, "Confuser.CLI.exe", "cli bytes"),
        };

        string previousFolder = workspace.Dir("Output", "MyTool_abc123");
        ModuleResult result = Module(workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), downloader)
            .CheckStart(new ModuleContext(
                TestFactory.Tool(name: "MyTool"), previousFolder, workspace.Paths.GitToolsPath));

        Assert.True(result.Status);
        (string url, string name, string path, string? sha) = Assert.Single(downloader.Requests);
        Assert.Equal("https://example.invalid/ConfuserEx-CLI.zip", url);
        Assert.Equal("ConfuserEx.zip", name);
        Assert.Equal(workspace.Paths.ResourcesPath, path);
        Assert.Equal("ab".PadRight(64, '0'), sha);

        Assert.Equal("cli bytes", File.ReadAllText(workspace.Paths.ConfuserExFile));

        // The archive is removed once unpacked, so a later run does not re-extract stale bytes.
        Assert.False(File.Exists(Path.Combine(workspace.Paths.ResourcesPath, "ConfuserEx.zip")));
    }

    [Fact]
    public void CheckStart_Fails_When_The_Archive_Does_Not_Contain_The_Cli()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        var ui = new RecordingConsoleUi();
        var downloader = new FakeResourceDownloader
        {
            OnDownload = destination => WriteZipContaining(destination, "readme.txt", "not the cli"),
        };

        ModuleResult result = Module(workspace, ui, new RecordingProcessRunner(), downloader)
            .CheckStart(new ModuleContext(
                TestFactory.Tool(name: "MyTool"),
                workspace.Dir("Output", "MyTool_abc123"),
                workspace.Paths.GitToolsPath));

        Assert.False(result.Status);
        Assert.Contains("File not found", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckStart_Fails_When_The_Download_Fails()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedResources();
        var ui = new RecordingConsoleUi();

        ModuleResult result = Module(
            workspace, ui, new RecordingProcessRunner(), new FakeResourceDownloader(succeed: false))
            .CheckStart(new ModuleContext(
                TestFactory.Tool(name: "MyTool"),
                workspace.Dir("Output", "MyTool_abc123"),
                workspace.Paths.GitToolsPath));

        Assert.False(result.Status);
        Assert.Contains("Download ERROR - ConfuserEx", ui.AllText, StringComparison.Ordinal);
    }

    private static void WriteZipContaining(string zipPath, string entryName, string content)
    {
        using var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        using Stream entry = archive.CreateEntry(entryName).Open();
        entry.Write(System.Text.Encoding.UTF8.GetBytes(content));
    }

    /// <summary>
    /// Referenced assemblies must be resolved against the tool's checkout. SolveDependences used
    /// the template's <em>relative</em> solutionPath as the reference root, so HintPaths resolved
    /// against the process working directory, nothing was ever found, and the obfuscated output
    /// shipped without the third-party assemblies it needs - silently, because a missing
    /// reference is simply skipped.
    /// </summary>
    [Fact]
    public void Referenced_Assemblies_Are_Resolved_Against_The_Checkout_And_Copied()
    {
        using var workspace = new TempWorkspace();
        ModuleContext context = Arrange(workspace, out string previousFolder);

        workspace.File("Git/MyTool/MyTool.sln", TestFactory.Solution("MyTool", "MyTool.csproj"));
        workspace.File("Git/MyTool/MyTool.csproj", """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <Reference Include="Vendor.Native">
                  <HintPath>lib\Vendor.Native.dll</HintPath>
                </Reference>
              </ItemGroup>
            </Project>
            """);
        workspace.File("Git/MyTool/lib/Vendor.Native.dll", "native bytes");

        Modules.ConfuserEx module = Module(
            workspace, new RecordingConsoleUi(), new RecordingProcessRunner(), new FakeResourceDownloader());
        module.CheckStart(context);

        module.Run(context);

        string copied = Path.Combine(previousFolder, "ConfuserEx", "Vendor.Native.dll");
        Assert.True(File.Exists(copied), $"expected the HintPath reference to be copied to {copied}");
        Assert.Equal("native bytes", File.ReadAllText(copied));
    }
}
