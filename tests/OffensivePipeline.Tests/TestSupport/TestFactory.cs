using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OffensivePipeline.Config;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Tests.TestSupport;

/// <summary>Construction helpers, so a test body is assertions rather than wiring.</summary>
internal static class TestFactory
{
    public static ILogger<T> Log<T>() => NullLogger<T>.Instance;

    public static YmlHelpers Yml(PipelinePaths paths, IConsoleUi ui) =>
        new(paths, ui, Log<YmlHelpers>());

    /// <summary>Paths rooted at the test assembly's own output folder, which holds the 79 templates.</summary>
    public static PipelinePaths ShippedPaths() => new(AppContext.BaseDirectory);

    /// <summary>
    /// Renders a tool template in exactly the shape the shipped ones use, so tests that need a
    /// synthetic tool exercise the same parser path as the real files.
    /// </summary>
    public static string Template(
        string name,
        string gitLink = "https://github.com/example/example",
        string solutionPath = @"Example\Example.sln",
        string plugins = "RandomGuid, BuildCsharp",
        string description = "An example tool.",
        string language = "c#",
        string authUser = "",
        string authToken = "",
        string toolArguments = "") =>
        $"""
        tool:
          - name: {name}
            description: {description}
            gitLink: {gitLink}
            solutionPath: {solutionPath}
            language: {language}
            plugins: {plugins}
            authUser: {authUser}
            authToken: {authToken}
            toolArguments: {toolArguments}

        """;

    /// <summary>A minimal, valid <see cref="ToolConfig"/> for tests that need one directly.</summary>
    public static ToolConfig Tool(
        string name = "Example",
        string gitLink = "https://github.com/example/example",
        string solutionPath = "Example/Example.sln",
        IReadOnlyList<string>? plugins = null,
        string toolArguments = "") =>
        new()
        {
            Name = name,
            Description = "An example tool.",
            GitLink = gitLink,
            SolutionPath = solutionPath,
            Language = "c#",
            Plugins = plugins ?? ["RandomGuid"],
            AuthUser = string.Empty,
            AuthToken = string.Empty,
            ToolArguments = toolArguments,
        };

    /// <summary>A minimal <c>.sln</c> naming one project, in the classic format the reader expects.</summary>
    public static string Solution(string projectName, string relativeProjectPath) =>
        "Microsoft Visual Studio Solution File, Format Version 12.00\r\n"
        + $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{projectName}\", "
        + $"\"{relativeProjectPath}\", \"{{102D65B0-DE68-4FBB-A34F-867CD97CB35F}}\"\r\n"
        + "EndProject\r\n"
        + "Global\r\nEndGlobal\r\n";
}
