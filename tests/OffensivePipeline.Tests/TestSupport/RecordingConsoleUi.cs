using OffensivePipeline.Ui;

namespace OffensivePipeline.Tests.TestSupport;

/// <summary>The colour role a line was written with, so tests can assert presentation intent.</summary>
public enum UiChannel
{
    Banner,
    Plain,
    Blank,
    Heading,
    Phase,
    Highlight,
    Detail,
    Success,
    Failure,
    Warning,
}

/// <summary>One recorded line of operator-facing output.</summary>
/// <param name="Channel">Which <see cref="IConsoleUi"/> method produced it.</param>
/// <param name="Text">The text handed to that method, before any prefix the real UI adds.</param>
public readonly record struct UiLine(UiChannel Channel, string Text);

/// <summary>
/// Captures everything the application would have printed. Nothing reaches the real console, so
/// tests never fight over <see cref="Console"/> state while xunit runs collections in parallel.
/// </summary>
public sealed class RecordingConsoleUi : IConsoleUi
{
    private readonly List<UiLine> _lines = [];

    public IReadOnlyList<UiLine> Lines => _lines;

    public IEnumerable<string> TextOf(UiChannel channel) =>
        _lines.Where(l => l.Channel == channel).Select(l => l.Text);

    /// <summary>Every line, regardless of channel, joined for substring assertions.</summary>
    public string AllText => string.Join('\n', _lines.Select(l => l.Text));

    public bool AnyContains(UiChannel channel, string fragment) =>
        TextOf(channel).Any(t => t.Contains(fragment, StringComparison.Ordinal));

    public void Banner(string text) => _lines.Add(new UiLine(UiChannel.Banner, text));

    public void Plain(string text) => _lines.Add(new UiLine(UiChannel.Plain, text));

    public void Blank() => _lines.Add(new UiLine(UiChannel.Blank, string.Empty));

    public void Heading(string text) => _lines.Add(new UiLine(UiChannel.Heading, text));

    public void Phase(string text) => _lines.Add(new UiLine(UiChannel.Phase, text));

    public void Highlight(string text) => _lines.Add(new UiLine(UiChannel.Highlight, text));

    public void Detail(string text) => _lines.Add(new UiLine(UiChannel.Detail, text));

    public void Success(string text) => _lines.Add(new UiLine(UiChannel.Success, text));

    public void Failure(string text) => _lines.Add(new UiLine(UiChannel.Failure, text));

    public void Warning(string text) => _lines.Add(new UiLine(UiChannel.Warning, text));
}
