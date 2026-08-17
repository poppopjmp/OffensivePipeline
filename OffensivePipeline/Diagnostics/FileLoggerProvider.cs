using Microsoft.Extensions.Logging;

namespace OffensivePipeline.Diagnostics;

/// <summary>
/// Appends diagnostics to <c>log.txt</c> through a single writer held open for the lifetime of the
/// process.
/// </summary>
/// <remarks>
/// The retired <c>LogHelpers.LogToFile</c> opened and closed a <see cref="FileStream"/> for every
/// line - thousands of times during an <c>all</c> run - and swallowed every exception, so a locked
/// log file silently produced nothing at all. One buffered writer replaces that; failure to open
/// the file is reported once instead of per line.
/// </remarks>
[ProviderAlias("File")]
public sealed class FileLoggerProvider(string logFilePath) : ILoggerProvider
{
    private readonly Lock _gate = new();
    private StreamWriter? _writer;
    private bool _openFailed;
    private bool _disposed;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>
    /// Flushes and closes the log file. Required before <c>clean</c> deletes <c>log.txt</c>, which
    /// would otherwise be held open by this provider. Logging afterwards reopens the file.
    /// </summary>
    public void CloseLogFile()
    {
        lock (_gate)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            _openFailed = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Appends one already-formatted line. Buffered; flushed when the entry is a warning or worse,
    /// so a crash cannot lose the diagnostics that explain it.
    /// </summary>
    internal void Write(string line, bool flush)
    {
        lock (_gate)
        {
            if (_disposed || _openFailed)
            {
                return;
            }

            if (_writer is null)
            {
                try
                {
                    string? directory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    _writer = new StreamWriter(
                        new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        AutoFlush = false,
                    };
                }
                catch (Exception ex)
                {
                    // Say so once and stop trying, rather than throwing on every log call. The
                    // previous implementation swallowed this silently, so a locked log file simply
                    // produced no diagnostics at all and nobody found out until they went looking.
                    _openFailed = true;
                    Console.Error.WriteLine(
                        $"[!] Could not open log file '{logFilePath}': {ex.Message}");
                    return;
                }
            }

            _writer.WriteLine(line);
            if (flush)
            {
                _writer.Flush();
            }
        }
    }

    /// <summary>Formats entries and hands them to the shared writer.</summary>
    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message} - {exception}";
            }

            // ISO-8601 so the log sorts and parses the same way in every locale; the previous
            // format was culture-dependent DateTime.Now.
            string timestamp = DateTimeOffset.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            provider.Write($"{timestamp} [{logLevel}] {category} -- {message}", logLevel >= LogLevel.Warning);
        }
    }
}
