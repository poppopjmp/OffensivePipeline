namespace OffensivePipeline.Ui;

/// <summary>
/// Writes the operator-facing output to the system console.
/// </summary>
/// <remarks>
/// The colour of every method is the colour the equivalent <c>LogHelpers.PrintX</c> call used, so
/// upgrading changes nothing an operator sees:
/// <list type="table">
///   <item><term>Failure</term><description>Red, prefixed <c>[ERROR]</c> (was <c>PrintError</c>)</description></item>
///   <item><term>Success</term><description>Green (was <c>PrintOk</c>)</description></item>
///   <item><term>Heading</term><description>Yellow (was <c>PrintYellow</c>)</description></item>
///   <item><term>Phase</term><description>Blue (was <c>PrintBlue</c>)</description></item>
///   <item><term>Highlight</term><description>Magenta (was <c>PrintMagenta</c>)</description></item>
///   <item><term>Detail</term><description>Gray (was <c>PrintGray</c>)</description></item>
///   <item><term>Plain / Banner</term><description>default (was a bare <c>Console.WriteLine</c>)</description></item>
/// </list>
/// </remarks>
public sealed class ConsoleUi : IConsoleUi
{
    public void Banner(string text) => Console.WriteLine(text);

    public void Plain(string text) => Console.WriteLine(text);

    public void Blank() => Console.WriteLine();

    public void Heading(string text) => Write(ConsoleColor.Yellow, text);

    public void Phase(string text) => Write(ConsoleColor.Blue, text);

    public void Highlight(string text) => Write(ConsoleColor.Magenta, text);

    public void Detail(string text) => Write(ConsoleColor.Gray, text);

    public void Success(string text) => Write(ConsoleColor.Green, text);

    public void Failure(string text) => Write(ConsoleColor.Red, $"[ERROR] {text}");

    public void Warning(string text) => Write(ConsoleColor.Yellow, $"[!] {text}");

    /// <summary>
    /// Colours a single line, leaving the console exactly as it was found. Colour is skipped when
    /// output is redirected, so a piped or captured run produces clean text.
    /// </summary>
    private static void Write(ConsoleColor color, string text)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(text);
            return;
        }

        Console.ForegroundColor = color;
        try
        {
            Console.WriteLine(text);
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
