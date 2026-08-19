using OffensivePipeline.Infrastructure;
using OffensivePipeline.Modules;

namespace OffensivePipeline.Tests.TestSupport;

/// <summary>
/// Records every command the pipeline would have run and answers with a canned result.
/// </summary>
/// <remarks>
/// The build and obfuscation modules are the two the tool exists for, and both are Windows-only at
/// run time. This seam is the only way their generated command lines and scripts get verified at
/// all on a machine with no Build Tools installed - and nothing is ever executed, so a test run can
/// never accidentally start MSBuild or ConfuserEx.
/// </remarks>
public sealed class RecordingProcessRunner(params int[] exitCodes) : IProcessRunner
{
    private readonly List<string> _commands = [];
    private int _call;

    /// <summary>The commands passed to <see cref="Run"/>, in order.</summary>
    public IReadOnlyList<string> Commands => _commands;

    public CommandResult Run(string command)
    {
        _commands.Add(command);
        int exitCode = _call < exitCodes.Length ? exitCodes[_call] : 0;
        _call++;
        return new CommandResult(exitCode, $"stdout {_call}", exitCode == 0 ? string.Empty : $"stderr {_call}");
    }
}

/// <summary>A downloader that never touches the network.</summary>
/// <param name="succeed">What <see cref="Download"/> reports.</param>
/// <param name="writeContent">
/// When set, the "downloaded" file is created with this content, so a caller that goes on to check
/// the file exists behaves as it would after a real download.
/// </param>
public sealed class FakeResourceDownloader(bool succeed = true, string? writeContent = null)
    : IResourceDownloader
{
    public List<(string Url, string Name, string Path, string? Sha256)> Requests { get; } = [];

    /// <summary>Invoked with the destination file path, to lay down a realistic payload.</summary>
    public Action<string>? OnDownload { get; init; }

    public bool Download(string url, string outputName, string outputPath, string? expectedSha256)
    {
        Requests.Add((url, outputName, outputPath, expectedSha256));
        if (!succeed)
        {
            return false;
        }

        Directory.CreateDirectory(outputPath);
        string destination = Path.Combine(outputPath, outputName);
        if (writeContent is not null)
        {
            File.WriteAllText(destination, writeContent);
        }

        OnDownload?.Invoke(destination);
        return true;
    }
}

/// <summary>
/// A git client that materialises a checkout locally instead of cloning, so cloning logic can be
/// exercised with no network access.
/// </summary>
/// <param name="populate">
/// Invoked with the destination path to lay down whatever the test needs; when null the destination
/// is not created at all, which is how a failed clone looks to <c>GitHelpers</c>.
/// </param>
public sealed class FakeGitClient(Action<string>? populate = null) : IGitClient
{
    public List<(string Url, string Destination, string? User, string? Token)> Clones { get; } = [];

    public void Clone(string sourceUrl, string destinationPath, string? userName, string? accessToken)
    {
        Clones.Add((sourceUrl, destinationPath, userName, accessToken));
        populate?.Invoke(destinationPath);
    }
}

/// <summary>A module whose behaviour each test dictates, and which records the contexts it saw.</summary>
public sealed class StubModule(string name, bool checkStartOk = true, bool runOk = true) : IModule
{
    public string Name { get; } = name;

    public List<ModuleContext> CheckStartCalls { get; } = [];

    public List<ModuleContext> RunCalls { get; } = [];

    /// <summary>When set, <see cref="Run"/> throws this instead of returning.</summary>
    public Exception? ThrowOnRun { get; init; }

    /// <summary>When set, <see cref="Run"/> reports this folder as its output.</summary>
    public string? OutputPathOverride { get; init; }

    public ModuleResult CheckStart(ModuleContext context)
    {
        CheckStartCalls.Add(context);
        return new ModuleResult
        {
            Name = Name,
            OutputPath = context.OutputPath,
            Status = checkStartOk,
        };
    }

    public ModuleResult Run(ModuleContext context)
    {
        RunCalls.Add(context);
        if (ThrowOnRun is not null)
        {
            throw ThrowOnRun;
        }

        return new ModuleResult
        {
            Name = Name,
            OutputPath = OutputPathOverride ?? context.OutputPath,
            Status = runOk,
        };
    }
}

/// <summary>Resolves the stub modules a test registered, and nothing else.</summary>
public sealed class StubModuleFactory(params StubModule[] modules) : IModuleFactory
{
    private readonly Dictionary<string, StubModule> _modules =
        modules.ToDictionary(m => m.Name, StringComparer.Ordinal);

    /// <summary>
    /// Defaults to the real registration list so plugin validation is asserted against the names
    /// the shipped templates actually use, not against whatever a test happened to register.
    /// </summary>
    public IReadOnlyList<string> KnownModules { get; init; } = ModuleNames.All;

    public IModule? Resolve(string name) =>
        _modules.TryGetValue(name, out StubModule? module) ? module : null;
}

/// <summary>The module names that are part of the <c>Tools/*.yml</c> public contract.</summary>
public static class ModuleNames
{
    public static readonly string[] All =
    [
        "RandomGuid",
        "RandomAssemblyInfo",
        "BuildCsharp",
        "ConfuserEx",
        "Donut",
    ];
}
