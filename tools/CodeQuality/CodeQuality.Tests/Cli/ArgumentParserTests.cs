using CodeQuality.Cli;
using Xunit;

namespace CodeQuality.Tests.Cli;

public class ArgumentParserTests
{
    // Ground truth: demonstrated harm was `--confg` (a typo for `--config`) being silently
    // ignored - the run fell back to no config at all with nothing in the output naming the
    // typo. An unrecognised flag must be rejected, and the message must name the exact flag text
    // so the typo is visible without re-reading the command line.
    [Fact]
    public void UnknownFlagThrowsNamingTheFlag()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(new[] { "summary", "/repo", "--confg", "rules.yml" }));

        Assert.Contains("--confg", exception.Message);
    }

    // Ground truth: demonstrated harm was `--min-lines` given with no following value silently
    // becoming 0 (every method matches), rather than being rejected the same way a value-less
    // --config or --verdict would be.
    [Fact]
    public void MinLinesWithNoFollowingValueThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(new[] { "methods", "/repo", "--min-lines" }));

        Assert.Contains("--min-lines", exception.Message);
    }

    [Fact]
    public void ConfigWithNoFollowingValueThrows() =>
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(new[] { "summary", "/repo", "--config" }));

    [Fact]
    public void VerdictWithNoFollowingValueThrows() =>
        Assert.Throws<ArgumentException>(() => ArgumentParser.Parse(new[] { "comments", "/repo", "--verdict" }));

    [Fact]
    public void NonNumericMinLinesThrows()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ArgumentParser.Parse(new[] { "methods", "/repo", "--min-lines", "abc" }));

        Assert.Contains("--min-lines", exception.Message);
    }

    [Fact]
    public void RecognisedFlagsParseIntoTheirFields()
    {
        var options = ArgumentParser.Parse(
            new[] { "methods", "/repo", "--json", "--config", "rules.yml", "--min-lines", "50", "--verdict", "Delete" });

        Assert.True(options.Json);
        Assert.Equal("rules.yml", options.Config);
        Assert.Equal(50, options.MinLines);
        Assert.Equal("Delete", options.Verdict);
    }

    [Fact]
    public void AbsentFlagsLeaveHarmlessDefaults()
    {
        var options = ArgumentParser.Parse(new[] { "summary", "/repo" });

        Assert.False(options.Json);
        Assert.Null(options.Config);
        Assert.Equal(0, options.MinLines);
        Assert.Null(options.Verdict);
    }
}
