using OffensivePipeline.Ui;

namespace OffensivePipeline.Tests;

/// <summary>
/// The real console writer. These tests replace <see cref="Console.Out"/>, which is process-wide
/// state, so they are pinned to a collection of their own that never runs alongside another.
/// </summary>
[Collection(nameof(ConsoleUiTests))]
[CollectionDefinition(nameof(ConsoleUiTests), DisableParallelization = true)]
public class ConsoleUiTests
{
    private static string Capture(Action<IConsoleUi> write)
    {
        TextWriter original = Console.Out;
        var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            write(new ConsoleUi());
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    [Fact]
    public void Errors_Are_Prefixed_So_They_Are_Greppable_In_A_Captured_Run() =>
        Assert.Equal(
            $"[ERROR] something went wrong{Environment.NewLine}",
            Capture(ui => ui.Failure("something went wrong")));

    [Fact]
    public void Warnings_Carry_The_Historic_Bang_Prefix() =>
        Assert.Equal(
            $"[!] deprecated{Environment.NewLine}", Capture(ui => ui.Warning("deprecated")));

    [Theory]
    [InlineData("heading")]
    [InlineData("phase")]
    [InlineData("highlight")]
    [InlineData("detail")]
    [InlineData("success")]
    public void Coloured_Channels_Emit_Their_Text_Unmodified(string channel)
    {
        string output = Capture(ui =>
        {
            switch (channel)
            {
                case "heading": ui.Heading("text"); break;
                case "phase": ui.Phase("text"); break;
                case "highlight": ui.Highlight("text"); break;
                case "detail": ui.Detail("text"); break;
                default: ui.Success("text"); break;
            }
        });

        Assert.Equal($"text{Environment.NewLine}", output);
    }

    [Fact]
    public void Plain_And_Banner_Text_Pass_Through_Verbatim()
    {
        Assert.Equal($"\tindented\ttext{Environment.NewLine}", Capture(ui => ui.Plain("\tindented\ttext")));
        Assert.Equal($"art{Environment.NewLine}", Capture(ui => ui.Banner("art")));
        Assert.Equal(Environment.NewLine, Capture(ui => ui.Blank()));
    }

    /// <summary>
    /// The console is left exactly as it was found, so a colour never bleeds into whatever the
    /// operator runs next.
    /// </summary>
    [Fact]
    public void The_Foreground_Colour_Is_Restored()
    {
        Assert.SkipWhen(Console.IsOutputRedirected, "Colour handling is bypassed when redirected.");

        ConsoleColor before = Console.ForegroundColor;
        Capture(ui => ui.Failure("boom"));

        Assert.Equal(before, Console.ForegroundColor);
    }
}
