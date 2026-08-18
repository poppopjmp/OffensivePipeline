using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Services;
using OffensivePipeline.Ui;

namespace OffensivePipeline;

internal sealed class Program
{
    /// <summary>
    /// The examples block that used to be <c>CommandLineApplication.ExtendedHelpText</c>. It is
    /// reproduced verbatim; System.CommandLine has no direct equivalent, so
    /// <see cref="ExtendedHelpAction"/> appends it to the root command's help.
    /// </summary>
    private static readonly string ExamplesHelpText = """
        Examples:
         - List all tools:
            OffensivePipeline.exe list
         - Load seatbelt tool:
            OffensivePipeline.exe t seatbelt [-a/--args] [args]
         - Load all tools:
            OffensivePipeline.exe all
         - Validate every template without building:
            OffensivePipeline.exe validate

        """.ReplaceLineEndings("\n");

    /// <summary>
    /// Reads the version the assembly was actually built with, so the banner can never disagree
    /// with the binary. It used to come from a hand-maintained <c>Version</c> app setting, which a
    /// stale config file could silently misreport.
    /// </summary>
    private static string GetVersion()
    {
        Assembly assembly = typeof(Program).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            // SourceLink appends "+<commit sha>" to the informational version.
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    /// <remarks>
    /// Internal rather than private so the test suite can assert the banner and the <c>@aetsu</c>
    /// credit are still emitted byte for byte; both are a hard constraint on this project.
    /// </remarks>
    internal static void ShowBanner(IConsoleUi ui)
    {
        // The banner is reproduced byte for byte. Line endings are normalised to CRLF and the
        // blank-looking separator line is spelled out as an escape, so neither the encoding of
        // this source file nor trailing-whitespace trimming can alter what is printed.
        string art = @"
                                                                                                   ooo
                                                                                           .osooooM M
      ___   __  __                _           ____  _            _ _                      +y.     M M
     / _ \ / _|/ _| ___ _ __  ___(_)_   _____|  _ \(_)_ __   ___| (_)_ __   ___           :h  .yoooMoM
    | | | | |_| |_ / _ \ '_ \/ __| \ \ / / _ \ |_) | | '_ \ / _ \ | | '_ \ / _ \          oo  oo
    | |_| |  _|  _|  __/ | | \__ \ |\ V /  __/  __/| | |_) |  __/ | | | | |  __/          oo  oo
     \___/|_| |_|  \___|_| |_|___/_| \_/ \___|_|   |_| .__/ \___|_|_|_| |_|\___|          oo  oo
                                                     |_|                            MoMoooy.  h:
                                                                                    M M     .y+
                                                                                    M Mooooso.
                                                                                    ooo".ReplaceLineEndings("\r\n");

        string credit = @"
                                                                    @aetsu
            ".ReplaceLineEndings("\r\n");

        string banner = art + "\r\n   " + credit
            + $"\t\t\t\t\t\t\t\t\tv{GetVersion()}\n";
        ui.Banner(banner);
    }

    private static int Main(string[] args)
    {
        IConsoleUi? ui = null;
        try
        {
            PipelinePaths paths = PipelinePaths.ForCurrentInstallation();
            IConfigurationRoot configuration = CompositionRoot.BuildConfiguration(paths);

            using ServiceProvider services = CompositionRoot.BuildServiceProvider(paths, configuration);
            ui = services.GetRequiredService<IConsoleUi>();

            ShowBanner(ui);
            LegacyAppConfigNotice.Report(paths, configuration, ui);

            return Invoke(services, args);
        }
        catch (Exception e)
        {
            // System.CommandLine's own handler is switched off below, so every escaping exception
            // lands here and becomes a reported failure with exit code 1. Previously an unhandled
            // exception aborted the runtime, which scripts saw as exit 134 plus a stack trace.
            ReportFatal(ui, e);
            return 1;
        }
    }

    private static void ReportFatal(IConsoleUi? ui, Exception e)
    {
        // The console gets the type and message - the type is often the whole diagnosis (an
        // UnauthorizedAccessException reads very differently from a FileNotFoundException) and
        // costs one line. The full stack trace goes to log.txt rather than the console so the
        // operator is pointed at it instead of being shown a wall of frames.
        string headline = $"{e.GetType().Name}: {e.Message}";

        if (ui is not null)
        {
            ui.Failure(headline);
        }
        else
        {
            // Thrown before the container existed, so there is no console UI to route this through.
            Console.Error.WriteLine($"[ERROR] {headline}");
        }

        TryAppendFatalToLog(e);
    }

    /// <summary>
    /// Appends the full exception to <c>log.txt</c>. Best effort: this runs while the process is
    /// already failing, and a container that never came up means there is no logger to use, so a
    /// failure to write must never mask the original error.
    /// </summary>
    private static void TryAppendFatalToLog(Exception e)
    {
        try
        {
            string logFile = PipelinePaths.ForCurrentInstallation().LogFile;
            File.AppendAllText(
                logFile,
                $"{DateTime.Now} [ERROR] Program - FATAL -- {e}{Environment.NewLine}");

            Console.Error.WriteLine($"[ERROR] Full details written to {logFile}");
        }
        catch (Exception logFailure)
        {
            Console.Error.WriteLine($"[ERROR] Could not write the fatal error to log.txt: {logFailure.Message}");
        }
    }

    private static int Invoke(IServiceProvider services, string[] args)
    {
        // The default handler prints a raw stack trace; Main's catch reports the message instead.
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        return BuildRootCommand(services).Parse(args).Invoke(invocation);
    }

    /// <summary>
    /// Builds the command tree. The verb surface is unchanged from every previous release:
    /// <c>list</c>, <c>all</c>, <c>t &lt;tool&gt; [-a|--args &lt;args&gt;]</c> and <c>clean</c>,
    /// with <c>-?</c>, <c>-h</c> and <c>--help</c> on the root and on every verb.
    /// </summary>
    /// <remarks>
    /// Separated from <see cref="Invoke"/> and made internal so the test suite can parse a command
    /// line and assert the bound values and the resulting exit code without any verb actually
    /// running - no cloning, no building, nothing written to disk.
    /// </remarks>
    internal static RootCommand BuildRootCommand(IServiceProvider services)
    {
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Echo diagnostic log output to the console as well as to log.txt.",
            Recursive = true,
        };

        // Given a description because the help layout always emits the "Description:" heading; an
        // empty one would leave a stray blank section above the usage line.
        var root = new RootCommand(
            "Download, build, obfuscate and generate shellcode from C# offensive tooling.");
        root.Options.Add(verboseOption);

        // -?, -h and --help are already this option's default aliases, an exact match for the
        // HelpOption("-?|-h|--help") the previous CLI declared, so nothing has to be reconstructed.
        HelpOption helpOption = root.Options.OfType<HelpOption>().First();
        var helpAction = new ExtendedHelpAction((SynchronousCommandLineAction)helpOption.Action!, root);
        helpOption.Action = helpAction;

        // The stock action prints the raw informational version, which SourceLink suffixes with the
        // commit sha. Reuse the banner's reading so the two can never disagree.
        root.Options.OfType<VersionOption>().First().Action = new PrintVersionAction();

        // A bare invocation has always printed help and exited 0. A root command with no action
        // would instead fail with "Required command was not provided" and exit 1, so the behaviour
        // is restored explicitly rather than left to the default.
        root.SetAction(helpAction.Invoke);

        void ApplyVerbosity(ParseResult parseResult)
        {
            if (parseResult.GetValue(verboseOption))
            {
                services.GetRequiredService<ConsoleVerbosity>().Verbose = true;
            }
        }

        var list = new Command("list", "List all tools");
        list.SetAction(parseResult =>
        {
            ApplyVerbosity(parseResult);
            services.GetRequiredService<ToolCatalog>().List();
            Console.WriteLine();
            return 0;
        });
        root.Subcommands.Add(list);

        var all = new Command("all", "Load all tools");
        all.SetAction(parseResult =>
        {
            ApplyVerbosity(parseResult);
            int failures = services.GetRequiredService<PipelineRunner>().Run();
            Console.WriteLine();
            return failures == 0 ? Success : Failure;
        });
        root.Subcommands.Add(all);

        // Required, so omitting it is now a parse error instead of silently succeeding.
        var toolArgument = new Argument<string>("tool") { Description = "Tool to build." };
        var toolArguments = new Option<string?>("--args", "-a")
        {
            Description = "Command-line arguments to pass to the Donut shellcode, will override the yaml value",
        };

        var tool = new Command("t", "Load the specified tool");
        tool.Arguments.Add(toolArgument);
        tool.Options.Add(toolArguments);
        tool.SetAction(parseResult =>
        {
            ApplyVerbosity(parseResult);
            int failures = services.GetRequiredService<PipelineRunner>()
                .Run(parseResult.GetRequiredValue(toolArgument), parseResult.GetValue(toolArguments));
            Console.WriteLine();
            return failures == 0 ? Success : Failure;
        });
        root.Subcommands.Add(tool);

        var clean = new Command("clean", "Clean all tools");
        clean.SetAction(parseResult =>
        {
            ApplyVerbosity(parseResult);
            services.GetRequiredService<WorkspaceCleaner>().Clean();
            Console.WriteLine();
            return Success;
        });
        root.Subcommands.Add(clean);

        var validate = new Command("validate", "Validate all tool templates without building");
        validate.SetAction(parseResult =>
        {
            ApplyVerbosity(parseResult);
            int invalid = services.GetRequiredService<TemplateValidator>().Validate();
            Console.WriteLine();
            return invalid == 0 ? Success : Failure;
        });
        root.Subcommands.Add(validate);

        return root;
    }

    private const int Success = 0;

    /// <summary>
    /// Any failure collapses to 1 rather than to a count, so the exit code stays inside the range a
    /// POSIX shell can report and never wraps to 0 on a multiple of 256.
    /// </summary>
    private const int Failure = 1;

    /// <summary>Prints the same version string the banner shows.</summary>
    private sealed class PrintVersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(GetVersion());
            return Success;
        }
    }

    /// <summary>
    /// Renders the standard help and then appends <see cref="ExamplesHelpText"/>, but only for the
    /// root command, matching where <c>ExtendedHelpText</c> used to appear.
    /// </summary>
    private sealed class ExtendedHelpAction(SynchronousCommandLineAction inner, Command root)
        : SynchronousCommandLineAction
    {
        public override bool Terminating => inner.Terminating;

        /// <summary>
        /// Delegated so that <c>t --help</c> still renders help rather than complaining about the
        /// missing required <c>tool</c> argument.
        /// </summary>
        public override bool ClearsParseErrors => inner.ClearsParseErrors;

        public override int Invoke(ParseResult parseResult)
        {
            int result = inner.Invoke(parseResult);
            if (ReferenceEquals(parseResult.CommandResult.Command, root))
            {
                parseResult.InvocationConfiguration.Output.Write(ExamplesHelpText);
            }

            return result;
        }
    }
}
