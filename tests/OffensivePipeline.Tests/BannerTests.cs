using System.Reflection;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// The banner. Preserving it - and the credit to the original author - is a hard constraint on
/// every change to this project, so it is asserted rather than left to review.
/// </summary>
public class BannerTests
{
    private static string Render()
    {
        var ui = new RecordingConsoleUi();
        Program.ShowBanner(ui);
        return Assert.Single(ui.TextOf(UiChannel.Banner));
    }

    [Fact]
    public void The_Credit_To_The_Original_Author_Is_Present() =>
        Assert.Contains("@aetsu", Render(), StringComparison.Ordinal);

    [Fact]
    public void The_Ascii_Art_Spells_Out_The_Project_Name()
    {
        string banner = Render();

        // The middle rows of the figlet lettering, which no reflow or trailing-whitespace trim can
        // survive unnoticed.
        Assert.Contains(
            @"     / _ \ / _|/ _| ___ _ __  ___(_)_   _____|  _ \(_)_ __   ___| (_)_ __   ___",
            banner,
            StringComparison.Ordinal);
        Assert.Contains(
            @"    | | | | |_| |_ / _ \ '_ \/ __| \ \ / / _ \ |_) | | '_ \ / _ \ | | '_ \ / _ \",
            banner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Line_Endings_Are_Normalised_So_The_Layout_Cannot_Drift()
    {
        string banner = Render();

        // Every newline in the art and credit is CRLF regardless of how this repository is checked
        // out; only the final version line ends with a bare LF, exactly as it always has.
        Assert.EndsWith("\n", banner, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\r", banner, StringComparison.Ordinal);
        Assert.Equal(banner.Split("\r\n").Length - 1, banner.Count(c => c == '\r'));
    }

    /// <summary>
    /// The version comes from the assembly, so a stale configuration file can no longer misreport
    /// which build is running.
    /// </summary>
    [Fact]
    public void The_Version_Matches_The_Assemblys_Own_Metadata()
    {
        Assembly assembly = typeof(Program).Assembly;
        string expected = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+')[0];

        Assert.Contains($"v{expected}", Render(), StringComparison.Ordinal);
    }

    /// <summary>SourceLink appends the commit sha to the informational version; it must be trimmed.</summary>
    [Fact]
    public void The_Version_Carries_No_Source_Revision_Suffix()
    {
        string versionLine = Render().Split('\n').Last(l => l.Contains('v', StringComparison.Ordinal));

        Assert.DoesNotContain('+', versionLine);
    }
}
