using System.Text.Json;
using System.Text.Json.Serialization;

namespace OffensivePipeline.Output;

/// <summary>
/// Serialises a report to stdout for <c>--json</c>. stdout carries the JSON document and nothing
/// else - the banner, warnings and progress go to stderr - so the output can be piped straight into
/// <c>jq</c> or a CI step.
/// </summary>
internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Writes <paramref name="model"/> as indented JSON to the real stdout.</summary>
    public static void Write<T>(T model) => Console.Out.WriteLine(Serialize(model));

    /// <summary>Exposed for tests, which assert the serialised shape without capturing the console.</summary>
    public static string Serialize<T>(T model) => JsonSerializer.Serialize(model, Options);
}
