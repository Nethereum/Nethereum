using System.Collections.Concurrent;
using CodeQuality.Core.Config;
using Xunit;

namespace CodeQuality.Tests.Config;

public class ConfigLoaderHostileInputTests
{
    [Fact]
    public void MalformedYamlDoesNotThrowAndFallsBackToDefault()
    {
        var config = ConfigLoader.LoadFrom("generated:\n  fileNameSuffixes: [\".gen.cs\"\n  bad: nested: colon\n");

        Assert.Empty(config.References.Vendors);
        Assert.True(config.Profiles.ContainsKey("default"));
    }

    [Fact]
    public void TabIndentedYamlDoesNotThrow()
    {
        var config = ConfigLoader.LoadFrom("generated:\n\tfileNameSuffixes: [\".gen.cs\"]\n");

        Assert.NotNull(config);
        Assert.NotEmpty(config.RulesVersion);
    }

    [Fact]
    public void EmptyFileDoesNotThrowAndYieldsDefaultProfile()
    {
        var config = ConfigLoader.LoadFrom(string.Empty);

        Assert.True(config.Profiles.ContainsKey("default"));
        Assert.Equal(35, config.Profiles["default"].MaxMethodLines);
    }

    [Fact]
    public void CommentOnlyYamlDoesNotThrow()
    {
        var config = ConfigLoader.LoadFrom("# nothing but a comment\n");

        Assert.Empty(config.References.Vendors);
    }

    [Fact]
    public void UnknownTopLevelKeyIsIgnoredNotFatal()
    {
        var config = ConfigLoader.LoadFrom("totallyUnknownKey: 5\nclones:\n  minStatements: 12\n");

        Assert.Equal(12, config.Clones.MinStatements);
    }

    [Fact]
    public void NullListValueIsNormalisedToEmptyNotNull()
    {
        var config = ConfigLoader.LoadFrom("references:\n  vendors:\n  citationPatterns: [\"x\"]\n");

        Assert.Empty(config.References.Vendors);
        Assert.Single(config.References.CitationPatterns);
    }

    [Fact]
    public void NullBindingsKeyResolvesToDefaultWithoutThrowing()
    {
        var config = ConfigLoader.LoadFrom("bindings:\n");

        Assert.Equal("default", ProfileResolver.Resolve(config, "/any/path.cs"));
    }

    [Fact]
    public void EmptyBindingsListResolvesToDefault()
    {
        var config = ConfigLoader.LoadFrom("bindings: []\n");

        Assert.Equal("default", ProfileResolver.Resolve(config, "/any/path.cs"));
    }

    // A binding naming a profile absent from `profiles:` is no longer tolerated input: it is a
    // configuration error the run must refuse rather than silently apply `default` under the
    // typo'd name. See ConfigLoaderTests for the full assertion of that behaviour; this file only
    // documents that the case moved out of "hostile but survivable".
    [Fact]
    public void BindingNamingAMissingProfileThrowsRatherThanBeingReturnedVerbatim()
    {
        var exception = Assert.Throws<ConfigurationException>(() =>
            ConfigLoader.LoadFrom("bindings:\n  - glob: \"**/*.cs\"\n    profile: doesNotExist\n"));

        Assert.Contains("doesNotExist", exception.Message);
    }

    [Theory]
    [InlineData("src/(weird+name).cs", "src/(weird+name).cs", true)]
    [InlineData("[unterminated", "[unterminated", true)]
    [InlineData("a.b.cs", "aXbXcs", false)]
    public void GlobsWithRegexMetacharactersAreTreatedLiterallyAndNeverThrow(string glob, string path, bool expectedMatch)
    {
        var config = ConfigLoader.LoadFrom(
            $"bindings:\n  - glob: \"{glob.Replace("\\", "\\\\")}\"\n    profile: hit\nprofiles:\n  hit:\n");

        var resolved = ProfileResolver.Resolve(config, path);

        Assert.Equal(expectedMatch ? "hit" : "default", resolved);
    }

    [Fact]
    public void EmptyGlobMatchesNothingRatherThanEverything()
    {
        var config = ConfigLoader.LoadFrom("bindings:\n  - glob: \"\"\n    profile: hit\nprofiles:\n  hit:\n");

        Assert.Equal("default", ProfileResolver.Resolve(config, "/any/path.cs"));
    }

    [Fact]
    public void ProfileEntryWithNoBodyDoesNotThrowAndUsesDefaults()
    {
        var config = ConfigLoader.LoadFrom("profiles:\n  bare:\n");

        Assert.True(config.Profiles.ContainsKey("bare"));
        Assert.Equal(35, config.Profiles["bare"].MaxMethodLines);
    }

    // Every call site loads config independently (a follower process, a test fixture, a CLI run);
    // nothing here shares a `ConfigLoader` instance across threads on purpose. But xUnit runs test
    // classes in parallel by default, and every `LoadFrom` call in the whole suite went through one
    // shared deserializer — enough concurrent callers reproduced a real, observed failure: a config
    // built from YAML that names "strict" coming back without a "strict" key. Two distinct YAML
    // documents, alternated across many threads, so a cross-thread mix-up shows up as a wrong key
    // or a wrong value rather than a coincidentally-matching one.
    [Fact]
    public void ConcurrentLoadFromAcrossManyThreadsNeverCrossesResults()
    {
        const string strictYaml = "profiles:\n  strict:\n    maxMethodLines: 11\n";
        const string lenientYaml = "profiles:\n  lenient:\n    maxMethodLines: 77\n";

        var failures = new ConcurrentBag<string>();

        Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            var expectStrict = i % 2 == 0;
            var config = ConfigLoader.LoadFrom(expectStrict ? strictYaml : lenientYaml);

            if (expectStrict)
            {
                if (!config.Profiles.TryGetValue("strict", out var profile) || profile.MaxMethodLines != 11)
                    failures.Add($"iteration {i}: expected strict/11, profiles were [{string.Join(",", config.Profiles.Keys)}]");
            }
            else
            {
                if (!config.Profiles.TryGetValue("lenient", out var profile) || profile.MaxMethodLines != 77)
                    failures.Add($"iteration {i}: expected lenient/77, profiles were [{string.Join(",", config.Profiles.Keys)}]");
            }
        });

        Assert.True(failures.Count == 0, $"{failures.Count} of 2000 concurrent loads returned the wrong config:\n{string.Join("\n", failures.Take(10))}");
    }
}
