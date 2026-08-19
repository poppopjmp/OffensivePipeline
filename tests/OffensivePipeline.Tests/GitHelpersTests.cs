using OffensivePipeline.Infrastructure;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// Getting a tool's source onto disk. The network call itself is behind <see cref="IGitClient"/>,
/// so everything that decides whether and where to clone is testable offline.
/// </summary>
public class GitHelpersTests
{
    private static GitHelpers Helpers(TempWorkspace workspace, RecordingConsoleUi ui, IGitClient client) =>
        new(client, ui, TestFactory.Log<GitHelpers>(), workspace.Paths);

    [Fact]
    public void A_Local_Tool_Is_Copied_Into_The_Git_Folder()
    {
        using var workspace = new TempWorkspace();
        string source = workspace.Dir("source");
        File.WriteAllText(Path.Combine(source, "MyTool.sln"), "solution");
        File.WriteAllText(Path.Combine(source, "readme.md"), "readme");

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, new FakeGitClient()).CloneLocalTool(
            TestFactory.Tool(name: "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln"));

        Assert.True(cloned);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "MyTool.sln")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.GitToolsPath, "MyTool", "readme.md")));
    }

    [Fact]
    public void A_Local_Tool_Whose_Solution_Is_Missing_Is_A_Failure()
    {
        using var workspace = new TempWorkspace();
        string source = workspace.Dir("source");
        File.WriteAllText(Path.Combine(source, "readme.md"), "readme");

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, new FakeGitClient()).CloneLocalTool(
            TestFactory.Tool(name: "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln"));

        Assert.False(cloned);
        Assert.Contains("CloneLocalTool", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Local_Tool_Whose_Source_Is_Missing_Is_A_Failure()
    {
        using var workspace = new TempWorkspace();

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, new FakeGitClient()).CloneLocalTool(
            TestFactory.Tool(
                name: "MyTool",
                gitLink: Path.Combine(workspace.Root, "no-such-source"),
                solutionPath: "MyTool/MyTool.sln"));

        Assert.False(cloned);
        Assert.Contains("Source directory not found", ui.AllText, StringComparison.Ordinal);
    }

    /// <summary>An existing checkout is removed first, so a run never builds a stale tree.</summary>
    [Fact]
    public void CloneChecks_Removes_A_Previous_Checkout()
    {
        using var workspace = new TempWorkspace();
        workspace.File("Git/MyTool/stale.txt", "from a previous run");

        var ui = new RecordingConsoleUi();
        bool ok = Helpers(workspace, ui, new FakeGitClient()).CloneChecks(TestFactory.Tool(name: "MyTool"));

        Assert.True(ok);
        Assert.False(Directory.Exists(Path.Combine(workspace.Paths.GitToolsPath, "MyTool")));
        Assert.True(Directory.Exists(workspace.Paths.GitToolsPath));
    }

    [Fact]
    public void CloneChecks_Creates_The_Git_Folder_When_It_Is_Absent()
    {
        using var workspace = new TempWorkspace();

        var ui = new RecordingConsoleUi();
        Assert.True(Helpers(workspace, ui, new FakeGitClient()).CloneChecks(TestFactory.Tool()));
        Assert.True(Directory.Exists(workspace.Paths.GitToolsPath));
    }

    [Fact]
    public void A_Remote_Tool_Is_Cloned_Into_A_Folder_Named_After_It()
    {
        using var workspace = new TempWorkspace();
        var client = new FakeGitClient(destination =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "MyTool.sln"), "solution");
        });

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, client).DownloadRepository(
            TestFactory.Tool(
                name: "MyTool",
                gitLink: "https://github.com/example/mytool",
                solutionPath: "MyTool/MyTool.sln"));

        Assert.True(cloned);
        (string url, string destination, _, _) = Assert.Single(client.Clones);
        Assert.Equal("https://github.com/example/mytool", url);
        Assert.Equal(Path.Combine(workspace.Paths.GitToolsPath, "MyTool"), destination);
    }

    [Fact]
    public void Credentials_From_The_Template_Are_Passed_To_The_Git_Client()
    {
        using var workspace = new TempWorkspace();
        var client = new FakeGitClient(destination =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "MyTool.sln"), "solution");
        });

        ToolConfig tool = TestFactory.Tool(
            name: "MyTool",
            gitLink: "https://github.example.com/private/mytool",
            solutionPath: "MyTool/MyTool.sln") with
        {
            AuthUser = "operator",
            AuthToken = "ghp_secret",
        };

        var ui = new RecordingConsoleUi();
        Assert.True(Helpers(workspace, ui, client).DownloadRepository(tool));

        (_, _, string? user, string? token) = Assert.Single(client.Clones);
        Assert.Equal("operator", user);
        Assert.Equal("ghp_secret", token);
    }

    /// <summary>Credentials must never reach the operator-facing output.</summary>
    [Fact]
    public void An_Auth_Token_Is_Never_Printed()
    {
        using var workspace = new TempWorkspace();
        var client = new FakeGitClient(destination =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "MyTool.sln"), "solution");
        });

        ToolConfig tool = TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/MyTool.sln") with
        {
            AuthUser = "operator",
            AuthToken = "ghp_secret",
        };

        var ui = new RecordingConsoleUi();
        Helpers(workspace, ui, client).DownloadRepository(tool);

        Assert.DoesNotContain("ghp_secret", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Clone_That_Produces_No_Solution_Is_A_Failure()
    {
        using var workspace = new TempWorkspace();

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, new FakeGitClient(destination => Directory.CreateDirectory(destination))).DownloadRepository(
            TestFactory.Tool(name: "MyTool", solutionPath: "MyTool/MyTool.sln"));

        Assert.False(cloned);
        Assert.Contains("Solution not found", ui.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Throwing_Git_Client_Is_Reported_Rather_Than_Propagated()
    {
        using var workspace = new TempWorkspace();
        var client = new FakeGitClient(_ => throw new InvalidOperationException("network is down"));

        var ui = new RecordingConsoleUi();
        bool cloned = Helpers(workspace, ui, client).DownloadRepository(TestFactory.Tool(name: "MyTool"));

        Assert.False(cloned);
        Assert.Contains("DownloadRepository", ui.AllText, StringComparison.Ordinal);
        Assert.Contains("network is down", ui.AllText, StringComparison.Ordinal);
    }
}
