using System.Text.Json;
using CodeQuality.Cli;
using Xunit;

namespace CodeQuality.Tests.Cli;

public class CommandsMethodsTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "cq-cli-" + Guid.NewGuid().ToString("N"));

    public CommandsMethodsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Ground truth, hand-counted from the literal text written below (signature line + open brace
    // + one line per statement + close brace): ShortMethod = 6, MediumMethod = 53, LongMethod =
    // 203. MediumMethod is deliberately over the default profile's 35-line long-method threshold
    // but under the 100-line --min-lines cutoff used below, so a test that only re-filters the
    // structure.long-method findings (the old behaviour) would still show it, while true --min-lines
    // filtering must not. This is what makes the assertions below fail for the OLD implementation
    // rather than pass by coincidence.
    void WriteFixture()
    {
        var mediumStatements = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"        var m{i} = {i};"));
        var longStatements = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"        var v{i} = {i};"));
        File.WriteAllText(Path.Combine(_root, "Thing.cs"), $$"""
            public class Thing
            {
                public void ShortMethod()
                {
                    var a = 1;
                    var b = 2;
                    var c = 3;
                }

                public void MediumMethod()
                {
            {{mediumStatements}}
                }

                public void LongMethod()
                {
            {{longStatements}}
                }
            }
            """);
    }

    static string CaptureOutput(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }
        return writer.ToString();
    }

    [Fact]
    public void MinLinesExcludesMethodsBelowTheThresholdEvenWhenTheyAreOtherwiseFlaggable()
    {
        WriteFixture();

        var output = CaptureOutput(() => Commands.Methods(_root, minLines: 100, json: false, configPath: null));

        Assert.Contains("LongMethod", output);
        Assert.DoesNotContain("ShortMethod", output);
        // MediumMethod (53 lines) exceeds the default profile's 35-line long-method threshold, so
        // the OLD "re-filter structure.long-method findings" behaviour would still print it here.
        // Excluding it is what proves --min-lines is actually applied, not just re-displaying the
        // rule engine's own verdicts.
        Assert.DoesNotContain("MediumMethod", output);
    }

    [Fact]
    public void AbsentMinLinesReturnsEveryMethod()
    {
        WriteFixture();

        var output = CaptureOutput(() => Commands.Methods(_root, minLines: 0, json: false, configPath: null));

        Assert.Contains("LongMethod", output);
        Assert.Contains("MediumMethod", output);
        Assert.Contains("ShortMethod", output);
    }

    [Fact]
    public void JsonFormCarriesTheLineCountMetric()
    {
        WriteFixture();

        var output = CaptureOutput(() => Commands.Methods(_root, minLines: 100, json: true, configPath: null));

        using var document = JsonDocument.Parse(output);
        var methods = document.RootElement.GetProperty("Methods").EnumerateArray().ToList();

        var longMethod = Assert.Single(methods);
        Assert.Equal("LongMethod", longMethod.GetProperty("Name").GetString());
        Assert.Equal(203, longMethod.GetProperty("LineCount").GetInt32());
    }

    // Ground truth: a bare array cannot carry the coverage counts summary --json exposes, so an
    // agent reading methods/clones/comments --json in isolation could not tell "0 unreadable
    // files" from "this command's output does not say". Every --json output now wraps its list in
    // the same envelope; this pins that the envelope's own coverage fields are present and
    // correct, not just that a "Coverage" key exists.
    [Fact]
    public void JsonFormCarriesTheCoverageEnvelope()
    {
        WriteFixture();

        var output = CaptureOutput(() => Commands.Methods(_root, minLines: 0, json: true, configPath: null));

        using var document = JsonDocument.Parse(output);
        var coverage = document.RootElement.GetProperty("Coverage");

        Assert.Equal(1, coverage.GetProperty("FilesAnalysed").GetInt32());
        Assert.Equal(0, coverage.GetProperty("FilesUnreadable").GetInt32());
        Assert.Equal(0, coverage.GetProperty("FilesWithDisabledRegions").GetInt32());
        Assert.Equal(JsonValueKind.Null, coverage.GetProperty("ConfigSourcePath").ValueKind);
    }

    [Fact]
    public void ResultsAreRankedByLineCountDescending()
    {
        var statements20 = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"        var a{i} = {i};"));
        var statements50 = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"        var b{i} = {i};"));
        File.WriteAllText(Path.Combine(_root, "Ranked.cs"), $$"""
            public class Ranked
            {
                public void Mid()
                {
            {{statements20}}
                }

                public void Big()
                {
            {{statements50}}
                }
            }
            """);

        var output = CaptureOutput(() => Commands.Methods(_root, minLines: 0, json: false, configPath: null));
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var bigIndex = Array.FindIndex(lines, l => l.Contains("Big"));
        var midIndex = Array.FindIndex(lines, l => l.Contains("Mid"));
        Assert.True(bigIndex >= 0 && midIndex >= 0 && bigIndex < midIndex,
            "Big (more lines) must be ranked ahead of Mid (fewer lines)");
    }
}
