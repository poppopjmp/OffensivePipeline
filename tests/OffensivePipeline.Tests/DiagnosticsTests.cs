using System.Globalization;
using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Infrastructure;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>The file log sink and the process runner.</summary>
public class DiagnosticsTests
{
    [Fact]
    public void Log_Lines_Carry_An_Iso8601_Timestamp_The_Level_And_The_Category()
    {
        using var workspace = new TempWorkspace();
        using (var provider = new FileLoggerProvider(workspace.Paths.LogFile))
        {
            provider.CreateLogger("OffensivePipeline.Modules.BuildCsharp").Info("Build completed");
        }

        string line = Assert.Single(File.ReadAllLines(workspace.Paths.LogFile));
        string[] parts = line.Split(" -- ", 2);

        Assert.Equal("Build completed", parts[1]);
        Assert.Contains("[Information]", parts[0], StringComparison.Ordinal);
        Assert.Contains("OffensivePipeline.Modules.BuildCsharp", parts[0], StringComparison.Ordinal);

        // Round-trippable regardless of the machine's locale; the old format was DateTime.Now.
        Assert.True(
            DateTimeOffset.TryParseExact(
                parts[0].Split(' ')[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            $"'{parts[0]}' should start with an ISO-8601 timestamp");
    }

    [Fact]
    public void Every_Level_Is_Written_And_Exceptions_Are_Appended()
    {
        using var workspace = new TempWorkspace();
        using (var provider = new FileLoggerProvider(workspace.Paths.LogFile))
        {
            ILogger logger = provider.CreateLogger("test");
            logger.Info("an info line");
            logger.Warn("a warning line");
            logger.Error(new InvalidOperationException("the cause"), "an error line");
        }

        string[] lines = File.ReadAllLines(workspace.Paths.LogFile);

        Assert.Equal(3, lines.Length);
        Assert.Contains("[Information]", lines[0], StringComparison.Ordinal);
        Assert.Contains("[Warning]", lines[1], StringComparison.Ordinal);
        Assert.Contains("[Error]", lines[2], StringComparison.Ordinal);
        Assert.Contains("the cause", lines[2], StringComparison.Ordinal);
    }

    /// <summary>Warnings and worse are flushed immediately, so a crash cannot lose them.</summary>
    [Fact]
    public void Warnings_Are_Flushed_Without_Waiting_For_Disposal()
    {
        using var workspace = new TempWorkspace();
        using var provider = new FileLoggerProvider(workspace.Paths.LogFile);

        provider.CreateLogger("test").Warn("something is wrong");

        using var stream = new FileStream(
            workspace.Paths.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        Assert.Contains("something is wrong", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Log_File_Reopens_After_Being_Closed()
    {
        using var workspace = new TempWorkspace();
        using var provider = new FileLoggerProvider(workspace.Paths.LogFile);
        ILogger logger = provider.CreateLogger("test");

        logger.Warn("before");
        provider.CloseLogFile();
        File.Delete(workspace.Paths.LogFile);

        logger.Warn("after");

        string content = File.ReadAllText(workspace.Paths.LogFile);
        Assert.Contains("after", content, StringComparison.Ordinal);
        Assert.DoesNotContain("before", content, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Log_Directory_Is_Created_On_Demand()
    {
        using var workspace = new TempWorkspace();
        string logFile = Path.Combine(workspace.Root, "nested", "deeper", "log.txt");

        using (var provider = new FileLoggerProvider(logFile))
        {
            provider.CreateLogger("test").Info("hello");
        }

        Assert.True(File.Exists(logFile));
    }

    /// <summary>
    /// An unopenable log file must not throw on every single call; the pipeline has to keep running.
    /// </summary>
    [Fact]
    public void An_Unopenable_Log_File_Does_Not_Throw()
    {
        using var workspace = new TempWorkspace();

        // A directory where the log file should be makes opening it impossible.
        Directory.CreateDirectory(workspace.Paths.LogFile);

        TextWriter originalError = Console.Error;
        try
        {
            Console.SetError(TextWriter.Null);
            using var provider = new FileLoggerProvider(workspace.Paths.LogFile);
            ILogger logger = provider.CreateLogger("test");

            logger.Info("first");
            logger.Info("second");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    // ---- ProcessRunner ---------------------------------------------------------------------

    /// <summary>
    /// A command that cannot even be started must be reported as a failure. On Linux this is every
    /// command, because the interpreter is <c>cmd.exe</c>; the point of the assertion is that the
    /// module chain stops rather than continuing on a phantom success.
    /// </summary>
    [Fact]
    public void A_Command_That_Cannot_Be_Started_Is_Not_A_Success()
    {
        var ui = new RecordingConsoleUi();
        var runner = new ProcessRunner(TestFactory.Log<ProcessRunner>(), ui);

        CommandResult result = runner.Run("definitely-not-a-real-executable-9f3c2a");

        if (OperatingSystem.IsWindows())
        {
            // cmd.exe starts fine and reports the unknown command itself.
            Assert.NotEqual(0, result.ExitCode);
        }
        else
        {
            Assert.Equal(-1, result.ExitCode);
            Assert.NotEmpty(ui.TextOf(UiChannel.Failure));
        }

        Assert.False(result.Succeeded);
    }

    [WindowsOnlyFact]
    public void A_Non_Zero_Exit_Code_Is_Surfaced()
    {
        var runner = new ProcessRunner(TestFactory.Log<ProcessRunner>(), new RecordingConsoleUi());

        CommandResult result = runner.Run("exit /b 3");

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Regression guard for the redirected-pipe deadlock: draining stdout to completion before
    /// touching stderr hangs as soon as the child fills the other pipe's buffer, which MSBuild does
    /// routinely.
    /// </summary>
    [WindowsOnlyFact]
    public void Large_Output_On_Both_Streams_Does_Not_Deadlock()
    {
        var runner = new ProcessRunner(TestFactory.Log<ProcessRunner>(), new RecordingConsoleUi());

        CommandResult result = runner.Run(
            "for /L %i in (1,1,2000) do @(echo out-%i& echo err-%i 1>&2)");

        Assert.True(result.Succeeded);
        Assert.Contains("out-2000", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("err-2000", result.StdErr, StringComparison.Ordinal);
    }

    // ---- ConsoleVerbosity ------------------------------------------------------------------

    [Fact]
    public void Verbosity_Is_Off_By_Default() => Assert.False(new ConsoleVerbosity().Verbose);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    public void Verbosity_Can_Be_Seeded_From_The_Environment(string? value, bool expected)
    {
        string? original = Environment.GetEnvironmentVariable(ConsoleVerbosity.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConsoleVerbosity.EnvironmentVariable, value);
            Assert.Equal(expected, ConsoleVerbosity.FromEnvironment().Verbose);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConsoleVerbosity.EnvironmentVariable, original);
        }
    }
}
