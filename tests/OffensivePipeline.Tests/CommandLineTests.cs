using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace OffensivePipeline.Tests;

/// <summary>
/// The command surface, asserted by parsing only. No verb action ever runs, so nothing clones,
/// builds or writes to disk - which also means these tests encode the exit-code contract without
/// needing a Windows box or a network.
/// </summary>
public class CommandLineTests
{
    /// <summary>
    /// An empty container is sufficient: parsing never resolves a service, and the one action these
    /// tests do invoke is the help renderer.
    /// </summary>
    private static RootCommand Root() =>
        Program.BuildRootCommand(new ServiceCollection().BuildServiceProvider());

    private static (int ExitCode, string Output) Invoke(params string[] args)
    {
        var output = new StringWriter();
        int exitCode = Root().Parse(args).Invoke(new InvocationConfiguration
        {
            Output = output,
            Error = output,
            EnableDefaultExceptionHandler = false,
        });

        return (exitCode, output.ToString());
    }

    [Fact]
    public void The_Verbs_Are_Present_And_Nothing_Else()
    {
        Assert.Equal(
            ["all", "clean", "list", "t", "validate"],
            Root().Subcommands.Select(c => c.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("all")]
    [InlineData("clean")]
    [InlineData("validate")]
    public void A_Bare_Verb_Parses_Without_Error(string verb) =>
        Assert.Empty(Root().Parse(verb).Errors);

    [Fact]
    public void The_Tool_Verb_Binds_Its_Argument()
    {
        Command tool = Root().Subcommands.Single(c => c.Name == "t");
        var argument = (Argument<string>)tool.Arguments.Single();

        ParseResult parsed = Root().Parse("t seatbelt");

        Assert.Empty(parsed.Errors);
        Assert.Equal("seatbelt", parsed.GetValue<string>(argument.Name));
    }

    [Theory]
    [InlineData("-a")]
    [InlineData("--args")]
    public void Both_Argument_Option_Aliases_Bind(string alias)
    {
        ParseResult parsed = Root().Parse(["t", "rubeus", alias, "-c All -d whatever.local"]);

        Assert.Empty(parsed.Errors);
        Assert.Equal("-c All -d whatever.local", parsed.GetValue<string>("--args"));
    }

    [Fact]
    public void The_Args_Option_Keeps_Its_Documented_Description()
    {
        Command tool = Root().Subcommands.Single(c => c.Name == "t");
        Option option = tool.Options.Single(o => o.Name == "--args");

        Assert.Contains("-a", option.Aliases);
        Assert.Equal(
            "Command-line arguments to pass to the Donut shellcode, will override the yaml value",
            option.Description);
    }

    [Fact]
    public void Omitting_The_Tool_Name_Is_A_Parse_Error()
    {
        ParseResult parsed = Root().Parse("t");

        Assert.NotEmpty(parsed.Errors);
        Assert.Equal(1, Invoke("t").ExitCode);
    }

    [Fact]
    public void An_Unknown_Verb_Is_Rejected_With_A_Message_And_Exit_One()
    {
        (int exitCode, string output) = Invoke("bogusverb");

        Assert.Equal(1, exitCode);
        Assert.NotEmpty(output);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Unknown_Option_Is_Rejected() =>
        Assert.NotEmpty(Root().Parse("list --nope").Errors);

    /// <summary>
    /// A bare invocation has always printed help and exited 0. A root command with no action would
    /// instead fail with "Required command was not provided".
    /// </summary>
    [Fact]
    public void A_Bare_Invocation_Prints_Help_And_Exits_Zero()
    {
        (int exitCode, string output) = Invoke();

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-?")]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Every_Help_Alias_Works_And_Lists_All_Four_Verbs(string alias)
    {
        (int exitCode, string output) = Invoke(alias);

        Assert.Equal(0, exitCode);
        foreach (string verb in (string[])["list", "all", "t", "clean"])
        {
            Assert.Contains(verb, output, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The examples block used to be <c>ExtendedHelpText</c>. It has to survive verbatim, because
    /// it is the only place <c>-a/--args</c> has ever been documented to an operator.
    /// </summary>
    [Fact]
    public void Root_Help_Still_Ends_With_The_Examples_Block()
    {
        (_, string output) = Invoke("--help");

        Assert.Contains(
            """
            Examples:
             - List all tools:
                OffensivePipeline.exe list
             - Load seatbelt tool:
                OffensivePipeline.exe t seatbelt [-a/--args] [args]
             - Load all tools:
                OffensivePipeline.exe all
            """,
            output.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    /// <summary>Help on a subcommand must render, not complain about the missing argument.</summary>
    [Fact]
    public void Help_On_The_Tool_Verb_Renders_Instead_Of_Demanding_The_Argument()
    {
        (int exitCode, string output) = Invoke("t", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--args", output, StringComparison.Ordinal);

        // The examples block belongs to the root command only.
        Assert.DoesNotContain("Examples:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Version_Option_Exists_And_Prints_A_Clean_Version()
    {
        (int exitCode, string output) = Invoke("--version");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain('+', output);
        Assert.Matches(@"^\d+\.\d+\.\d+", output.Trim());
    }

    [Fact]
    public void The_Verbose_Flag_Is_Available_On_Every_Verb()
    {
        foreach (string verb in (string[])["list", "all", "clean"])
        {
            Assert.Empty(Root().Parse([verb, "--verbose"]).Errors);
        }

        Assert.Empty(Root().Parse(["t", "seatbelt", "--verbose"]).Errors);
    }
}
