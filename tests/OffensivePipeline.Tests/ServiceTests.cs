using OffensivePipeline.Diagnostics;
using OffensivePipeline.Modules;
using OffensivePipeline.Services;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>The three verb implementations, driven entirely through fakes.</summary>
public class ServiceTests
{
    private static ToolCatalog Catalog(TempWorkspace workspace, RecordingConsoleUi ui) =>
        new(ui, TestFactory.Yml(workspace.Paths, ui));

    private static WorkspaceCleaner Cleaner(
        TempWorkspace workspace, RecordingConsoleUi ui, FileLoggerProvider provider) =>
        new(ui, TestFactory.Log<WorkspaceCleaner>(), workspace.Paths, provider);

    private static PipelineRunner Runner(
        TempWorkspace workspace, RecordingConsoleUi ui, IModuleFactory factory) =>
        new(
            ui,
            TestFactory.Log<PipelineRunner>(),
            workspace.Paths,
            TestFactory.Yml(workspace.Paths, ui),
            new GitHelpers(new FakeGitClient(), ui, TestFactory.Log<GitHelpers>(), workspace.Paths),
            factory);

    private static void WriteTemplate(TempWorkspace workspace, string name, string content)
    {
        Directory.CreateDirectory(workspace.Paths.YmlsPath);
        File.WriteAllText(Path.Combine(workspace.Paths.YmlsPath, $"{name}.yml"), content);
    }

    /// <summary>A local "repository" the runner can copy from, containing the declared solution.</summary>
    private static string LocalSource(TempWorkspace workspace, string toolName)
    {
        string source = workspace.Dir("source", toolName);
        File.WriteAllText(Path.Combine(source, $"{toolName}.sln"), "solution");
        return source;
    }

    // ---- ToolCatalog -----------------------------------------------------------------------

    [Fact]
    public void The_Listing_Numbers_Every_Tool_And_Shows_Its_Details()
    {
        using var workspace = new TempWorkspace();
        WriteTemplate(workspace, "Alpha", TestFactory.Template(
            "Alpha", description: "First tool.", plugins: "RandomGuid, Donut"));
        WriteTemplate(workspace, "Bravo", TestFactory.Template("Bravo", description: "Second tool."));

        var ui = new RecordingConsoleUi();
        Catalog(workspace, ui).List();

        Assert.Equal(["[1/2] Alpha - <c#>:", "[2/2] Bravo - <c#>:"], ui.TextOf(UiChannel.Heading));
        Assert.Contains("\t> Description: First tool.", ui.TextOf(UiChannel.Plain));
        Assert.Contains("\t> Link: https://github.com/example/example", ui.TextOf(UiChannel.Plain));
        Assert.Contains("\t> Plugins: RandomGuid, Donut", ui.TextOf(UiChannel.Phase));
    }

    [Fact]
    public void An_Empty_Tools_Folder_Lists_Nothing_Without_Throwing()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);

        var ui = new RecordingConsoleUi();
        Catalog(workspace, ui).List();

        Assert.Empty(ui.TextOf(UiChannel.Heading));
    }

    // ---- WorkspaceCleaner ------------------------------------------------------------------

    [Fact]
    public void Cleaning_Removes_The_Working_Folders_And_Recreates_Them_Empty()
    {
        using var workspace = new TempWorkspace();
        workspace.File("Git/MyTool/checkout.txt", "stale");
        workspace.File("Output/MyTool_abc/payload.bin", "stale");

        using var provider = new FileLoggerProvider(workspace.Paths.LogFile);
        Cleaner(workspace, new RecordingConsoleUi(), provider).Clean();

        Assert.True(Directory.Exists(workspace.Paths.GitToolsPath));
        Assert.True(Directory.Exists(workspace.Paths.OutputPath));
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Paths.GitToolsPath));
        Assert.Empty(Directory.GetFileSystemEntries(workspace.Paths.OutputPath));
    }

    /// <summary>
    /// The log file is held open for the process lifetime, so <c>clean</c> has to let go of it
    /// before deleting - otherwise the delete fails on Windows and the log grows for ever.
    /// </summary>
    [Fact]
    public void Cleaning_Deletes_The_Log_File_Even_Though_The_Logger_Holds_It_Open()
    {
        using var workspace = new TempWorkspace();
        using var provider = new FileLoggerProvider(workspace.Paths.LogFile);
        provider.CreateLogger("test").Info("something happened");

        Assert.True(File.Exists(workspace.Paths.LogFile));

        Cleaner(workspace, new RecordingConsoleUi(), provider).Clean();

        Assert.False(File.Exists(workspace.Paths.LogFile));
    }

    [Fact]
    public void Cleaning_An_Already_Clean_Installation_Is_Not_An_Error()
    {
        using var workspace = new TempWorkspace();
        using var provider = new FileLoggerProvider(workspace.Paths.LogFile);

        var ui = new RecordingConsoleUi();
        Cleaner(workspace, ui, provider).Clean();

        Assert.Empty(ui.TextOf(UiChannel.Failure));
        Assert.True(Directory.Exists(workspace.Paths.GitToolsPath));
    }

    // ---- PipelineRunner --------------------------------------------------------------------

    [Fact]
    public void A_Successful_Run_Reports_No_Failures_And_Runs_Every_Plugin_In_Order()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln",
            plugins: "RandomGuid, BuildCsharp"));

        var first = new StubModule("RandomGuid");
        var second = new StubModule("BuildCsharp");
        var ui = new RecordingConsoleUi();

        int failures = Runner(workspace, ui, new StubModuleFactory(first, second)).Run();

        Assert.Equal(0, failures);
        Assert.Single(first.RunCalls);
        Assert.Single(second.RunCalls);
        Assert.Contains("\t - RandomGuid: OK", ui.TextOf(UiChannel.Success));
        Assert.Contains("\t - BuildCsharp: OK", ui.TextOf(UiChannel.Success));
    }

    /// <summary>
    /// Once a stage fails the rest of the chain is skipped, but every declared plugin is still
    /// listed in the summary - as an error, not silently omitted.
    /// </summary>
    [Fact]
    public void A_Failing_Module_Stops_The_Chain_And_Fails_The_Tool()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln",
            plugins: "RandomGuid, BuildCsharp, Donut"));

        var failing = new StubModule("BuildCsharp", runOk: false);
        var never = new StubModule("Donut");
        var ui = new RecordingConsoleUi();

        int failures = Runner(
            workspace, ui, new StubModuleFactory(new StubModule("RandomGuid"), failing, never)).Run();

        Assert.Equal(1, failures);
        Assert.Empty(never.RunCalls);
        Assert.Empty(never.CheckStartCalls);
        Assert.Contains("\t - BuildCsharp: ERROR", ui.TextOf(UiChannel.Failure));
        Assert.Contains("\t - Donut: ERROR", ui.TextOf(UiChannel.Failure));
    }

    [Fact]
    public void A_Module_That_Fails_Its_Prerequisites_Is_Never_Run()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln", plugins: "BuildCsharp"));

        var module = new StubModule("BuildCsharp", checkStartOk: false);

        int failures = Runner(workspace, new RecordingConsoleUi(), new StubModuleFactory(module)).Run();

        Assert.Equal(1, failures);
        Assert.Single(module.CheckStartCalls);
        Assert.Empty(module.RunCalls);
    }

    [Fact]
    public void A_Throwing_Module_Is_Reported_As_A_Failure_Not_Propagated()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln", plugins: "Donut"));

        var module = new StubModule("Donut") { ThrowOnRun = new InvalidOperationException("boom") };
        var ui = new RecordingConsoleUi();

        int failures = Runner(workspace, ui, new StubModuleFactory(module)).Run();

        Assert.Equal(1, failures);
        Assert.Contains("boom", ui.AllText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Modules that nest their output hand the next stage the folder they actually wrote to.
    /// </summary>
    [Fact]
    public void The_Output_Folder_Is_Threaded_From_One_Module_To_The_Next()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln",
            plugins: "ConfuserEx, Donut"));

        string nested = Path.Combine(workspace.Paths.OutputPath, "nested-by-confuser");
        var confuser = new StubModule("ConfuserEx") { OutputPathOverride = nested };
        var donut = new StubModule("Donut");

        Runner(workspace, new RecordingConsoleUi(), new StubModuleFactory(confuser, donut)).Run();

        Assert.Equal(nested, Assert.Single(donut.RunCalls).OutputPath);
    }

    [Fact]
    public void An_Unresolvable_Plugin_Is_Reported_Before_Any_Work_Starts()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln", plugins: "NoSuchModule"));

        var ui = new RecordingConsoleUi();
        int failures = Runner(workspace, ui, new StubModuleFactory()).Run();

        Assert.Equal(1, failures);
        Assert.Contains(
            "unknown plugin 'NoSuchModule' in tool 'MyTool'",
            ui.TextOf(UiChannel.Failure));
    }

    [Fact]
    public void An_Unknown_Tool_Name_Is_A_Failure()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Paths.YmlsPath);

        var ui = new RecordingConsoleUi();
        int failures = Runner(workspace, ui, new StubModuleFactory()).Run("doesnotexist");

        Assert.Equal(1, failures);
        Assert.Contains("not found", ui.AllText, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tool whose source never arrived used to be recorded with an empty module list and a
    /// status of true, so the run reported success.
    /// </summary>
    [Fact]
    public void A_Tool_Whose_Source_Cannot_Be_Fetched_Is_A_Failure()
    {
        using var workspace = new TempWorkspace();
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool",
            gitLink: Path.Combine(workspace.Root, "no-such-source"),
            solutionPath: "MyTool/MyTool.sln"));

        var module = new StubModule("RandomGuid");
        int failures = Runner(workspace, new RecordingConsoleUi(), new StubModuleFactory(module)).Run();

        Assert.Equal(1, failures);
        Assert.Empty(module.CheckStartCalls);
    }

    [Fact]
    public void Failures_Are_Counted_Per_Tool_Across_A_Whole_Run()
    {
        using var workspace = new TempWorkspace();
        WriteTemplate(workspace, "Good", TestFactory.Template(
            "Good", gitLink: LocalSource(workspace, "Good"), solutionPath: "Good/Good.sln",
            plugins: "RandomGuid"));
        WriteTemplate(workspace, "Bad", TestFactory.Template(
            "Bad", gitLink: Path.Combine(workspace.Root, "no-such-source"),
            solutionPath: "Bad/Bad.sln", plugins: "RandomGuid"));
        WriteTemplate(workspace, "AlsoBad", TestFactory.Template(
            "AlsoBad", gitLink: Path.Combine(workspace.Root, "also-no-such-source"),
            solutionPath: "AlsoBad/AlsoBad.sln", plugins: "RandomGuid"));

        int failures = Runner(
            workspace, new RecordingConsoleUi(), new StubModuleFactory(new StubModule("RandomGuid"))).Run();

        Assert.Equal(2, failures);
    }

    [Fact]
    public void The_Tool_Argument_Override_Reaches_The_Module()
    {
        using var workspace = new TempWorkspace();
        string source = LocalSource(workspace, "MyTool");
        WriteTemplate(workspace, "MyTool", TestFactory.Template(
            "MyTool", gitLink: source, solutionPath: "MyTool/MyTool.sln",
            plugins: "Donut", toolArguments: "from-template"));

        var donut = new StubModule("Donut");
        Runner(workspace, new RecordingConsoleUi(), new StubModuleFactory(donut))
            .Run("MyTool", "-c All -d whatever.local");

        Assert.Equal("-c All -d whatever.local", Assert.Single(donut.RunCalls).Tool.ToolArguments);
    }

    [Fact]
    public void A_Summary_Is_Printed_For_Every_Tool()
    {
        using var workspace = new TempWorkspace();
        WriteTemplate(workspace, "Alpha", TestFactory.Template(
            "Alpha", gitLink: LocalSource(workspace, "Alpha"), solutionPath: "Alpha/Alpha.sln",
            plugins: "RandomGuid"));
        WriteTemplate(workspace, "Bravo", TestFactory.Template(
            "Bravo", gitLink: LocalSource(workspace, "Bravo"), solutionPath: "Bravo/Bravo.sln",
            plugins: "RandomGuid"));

        var ui = new RecordingConsoleUi();
        Runner(workspace, ui, new StubModuleFactory(new StubModule("RandomGuid"))).Run();

        Assert.Contains("\t\tSUMMARY\n", ui.TextOf(UiChannel.Highlight));
        Assert.Contains(" - Alpha", ui.TextOf(UiChannel.Plain));
        Assert.Contains(" - Bravo", ui.TextOf(UiChannel.Plain));
    }
}
