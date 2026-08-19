using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Infrastructure;
using OffensivePipeline.Modules;
using OffensivePipeline.Services;
using OffensivePipeline.Ui;

namespace OffensivePipeline;

/// <summary>
/// Builds the application's configuration and service container.
/// </summary>
/// <remarks>
/// A plain <see cref="ServiceCollection"/> rather than a generic host: this is a one-shot CLI, and
/// a host lifetime loop with console-lifetime signal handling would only get in the way of the
/// process returning a meaningful exit code.
/// </remarks>
internal static class CompositionRoot
{
    /// <summary>
    /// Configuration sources, lowest precedence first: the shipped <c>appsettings.json</c>
    /// defaults, then the deprecated <c>OffensivePipeline.dll.config</c>, then an optional
    /// gitignored <c>appsettings.Local.json</c> for machine-specific overrides.
    /// </summary>
    /// <remarks>
    /// The legacy file sits <em>above</em> the shipped defaults on purpose. It only exists on a
    /// machine where an operator edited it - typically to point <c>BuildCSharpTools</c> at their
    /// own Build Tools install - and <c>appsettings.json</c> ships with every key populated. Were
    /// the legacy file lower, its values could never win, and upgrading would silently drop that
    /// customisation. It stays deprecated and is removed in v3.1.0.
    /// </remarks>
    public static IConfigurationRoot BuildConfiguration(PipelinePaths paths) =>
        new ConfigurationBuilder()
            .AddJsonFile(paths.AppSettingsFile, optional: true, reloadOnChange: false)
            .Add(new LegacyAppConfigSource(paths.LegacyAppConfigFile))
            .AddJsonFile(paths.LocalAppSettingsFile, optional: true, reloadOnChange: false)
            .Build();

    public static ServiceProvider BuildServiceProvider(
        PipelinePaths paths, IConfiguration configuration, bool jsonMode = false)
    {
        var services = new ServiceCollection();

        services.AddSingleton(paths);
        services.AddSingleton(configuration);

        // In --json mode every human line goes to stderr, leaving stdout for the JSON document
        // alone so the output pipes cleanly into jq or a CI step.
        if (jsonMode)
        {
            services.AddSingleton<IConsoleUi>(ConsoleUi.ToStandardError());
        }
        else
        {
            services.AddSingleton<IConsoleUi, ConsoleUi>();
        }

        var verbosity = ConsoleVerbosity.FromEnvironment();
        services.AddSingleton(verbosity);

        var fileLoggerProvider = new FileLoggerProvider(paths.LogFile);
        services.AddSingleton(fileLoggerProvider);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);

            // Diagnostics always go to log.txt. They reach the console only when the operator asks,
            // because they would otherwise bury the progress tree the pipeline prints.
            builder.AddProvider(fileLoggerProvider);

            // In --json mode every console log line must go to stderr as well, otherwise
            // 'list --json --verbose' interleaves logger output with the JSON document and stdout
            // stops parsing. Redirecting IConsoleUi alone is not enough: the console logger writes
            // to Console.Out independently of it.
            if (jsonMode)
            {
                builder.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            }
            else
            {
                builder.AddConsole();
            }

            builder.AddFilter<ConsoleLoggerProvider>((_, _) => verbosity.Verbose);
        });

        // Bound once, lazily, so that 'list' and 'clean' keep working on an installation whose
        // appsettings.json is missing or incomplete - neither verb reads any of these settings.
        services.AddSingleton(serviceProvider =>
        {
            PipelineOptions options = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetSection(PipelineOptions.SectionName)
                .Get<PipelineOptions>() ?? new PipelineOptions();

            IReadOnlyList<string> missing = options.FindMissingSettings();
            return missing.Count == 0
                ? options
                : throw new InvalidOperationException(
                    $"Missing required setting(s) {string.Join(", ", missing)} in appsettings.json.");
        });

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IResourceDownloader, ResourceDownloader>();
        services.AddSingleton<IGitClient, GitClient>();

        services.AddSingleton<YmlHelpers>();
        services.AddSingleton<GitHelpers>();
        services.AddSingleton<IModuleFactory, ModuleFactory>();

        // The keys are the literal strings a template's 'plugins:' field may contain. They are the
        // public contract of every Tools/*.yml file, so they must never be renamed.
        services.AddKeyedTransient<IModule, RandomGuid>("RandomGuid");
        services.AddKeyedTransient<IModule, RandomAssemblyInfo>("RandomAssemblyInfo");
        services.AddKeyedTransient<IModule, BuildCsharp>("BuildCsharp");
        services.AddKeyedTransient<IModule, Modules.ConfuserEx>("ConfuserEx");
        services.AddKeyedTransient<IModule, Modules.Donut>("Donut");

        services.AddSingleton<ToolCatalog>();
        services.AddSingleton<WorkspaceCleaner>();
        services.AddSingleton<PipelineRunner>();
        services.AddSingleton<TemplateValidator>();

        return services.BuildServiceProvider();
    }
}
