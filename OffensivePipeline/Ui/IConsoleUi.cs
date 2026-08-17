namespace OffensivePipeline.Ui;

/// <summary>
/// Everything the operator sees on stdout: the banner, the tool listing, the progress tree and the
/// run summary.
/// </summary>
/// <remarks>
/// This is presentation, deliberately kept separate from <c>ILogger</c> diagnostics. Routing the
/// listing or the progress tree through a logger would prepend categories and levels and destroy
/// the tab-indented layout, so the two concerns never share a sink. Methods are named for intent;
/// the colour each one maps to is fixed by <see cref="ConsoleUi"/> and reproduces the output of
/// every previous release exactly.
/// </remarks>
public interface IConsoleUi
{
    /// <summary>Writes the ASCII banner verbatim, in the terminal's default colour.</summary>
    void Banner(string text);

    /// <summary>Writes an uncoloured line, such as a tool description or a summary entry.</summary>
    void Plain(string text);

    /// <summary>Writes an empty line.</summary>
    void Blank();

    /// <summary>Yellow. A tool listing entry, or an announcement that work is about to start.</summary>
    void Heading(string text);

    /// <summary>Blue. The pipeline phase currently executing.</summary>
    void Phase(string text);

    /// <summary>Magenta. Tool load announcements and the summary header.</summary>
    void Highlight(string text);

    /// <summary>Gray. Per-file and per-path detail beneath a phase.</summary>
    void Detail(string text);

    /// <summary>Green. A step completed successfully.</summary>
    void Success(string text);

    /// <summary>Red, prefixed <c>[ERROR]</c>. A step failed.</summary>
    void Failure(string text);

    /// <summary>Yellow, prefixed <c>[!]</c>. A deprecation or configuration warning.</summary>
    void Warning(string text);
}
