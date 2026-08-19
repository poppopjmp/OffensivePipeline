using Microsoft.Extensions.Logging;

namespace OffensivePipeline.Diagnostics;

/// <summary>
/// The diagnostic logging surface, built on cached <see cref="LoggerMessage"/> delegates.
/// </summary>
/// <remarks>
/// <para>
/// The <c>ILogger.LogInformation</c>-style extension methods box their arguments and re-parse the
/// message template on every call, which is what CA1848 objects to. Defining the delegates once
/// does the template parsing at startup instead.
/// </para>
/// <para>
/// The pipeline's diagnostics are whole sentences assembled at the call site rather than templates
/// with many named fields, so a single <c>{Message}</c> template per level covers every call and
/// keeps <c>log.txt</c> readable in the same way earlier releases did. Anything the operator is
/// meant to read on screen goes through <c>IConsoleUi</c>, not through here.
/// </para>
/// </remarks>
internal static class PipelineLog
{
    private static readonly Action<ILogger, string, Exception?> InfoCallback =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1000, "Info"), "{Message}");

    private static readonly Action<ILogger, string, Exception?> WarnCallback =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1001, "Warning"), "{Message}");

    private static readonly Action<ILogger, string, Exception?> ErrorCallback =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1002, "Error"), "{Message}");

    public static void Info(this ILogger logger, string message) =>
        InfoCallback(logger, message, null);

    public static void Warn(this ILogger logger, string message) =>
        WarnCallback(logger, message, null);

    public static void Error(this ILogger logger, string message) =>
        ErrorCallback(logger, message, null);

    public static void Error(this ILogger logger, Exception exception, string message) =>
        ErrorCallback(logger, message, exception);
}
