using CodeQuality.Core.Config;
using Xunit;

namespace CodeQuality.Tests.Config;

public class ConfigLoaderTests
{
    const string Yaml = """
        generated:
          fileNameSuffixes: [".gen.cs"]
          headerMarkers: ["auto-generated"]
        clones:
          minStatements: 8
        references:
          vendors: ["acme", "widgetlib"]
          sourcePointerPatterns: ["\\.(go|py|rs)\\b", ":\\d+"]
          internalSymbolPatterns: ["\\b[a-z]+[A-Z][A-Za-z]*\\("]
          citationPatterns: ["SPEC-\\d+"]
        profiles:
          strict:
            maxMethodLines: 20
            maxNesting: 2
            referencesAlwaysDefect: true
          default:
            maxMethodLines: 35
        bindings:
          - glob: "**/Core/**"
            profile: strict
        """;

    [Fact]
    public void ReadsThresholdsFromYaml()
    {
        var config = ConfigLoader.LoadFrom(Yaml);

        Assert.Equal(20, config.Profiles["strict"].MaxMethodLines);
        Assert.Equal(2, config.Profiles["strict"].MaxNesting);
        Assert.True(config.Profiles["strict"].ReferencesAlwaysDefect);
    }

    [Fact]
    public void ReadsVendorAndPatternLists()
    {
        var config = ConfigLoader.LoadFrom(Yaml);

        Assert.Contains("acme", config.References.Vendors);
        Assert.Contains("SPEC-\\d+", config.References.CitationPatterns);
    }

    [Fact]
    public void DefaultConfigCarriesNoDomainKnowledge()
    {
        var config = ConfigLoader.LoadFrom("clones:\n  minStatements: 8");

        Assert.Empty(config.References.Vendors);
        Assert.Empty(config.References.CitationPatterns);
    }

    [Fact]
    public void RulesVersionChangesWhenAnyValueChanges()
    {
        var first = ConfigLoader.LoadFrom(Yaml).RulesVersion;
        var second = ConfigLoader.LoadFrom(Yaml.Replace("maxMethodLines: 20", "maxMethodLines: 21")).RulesVersion;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RulesVersionIsStableForIdenticalConfig() =>
        Assert.Equal(ConfigLoader.LoadFrom(Yaml).RulesVersion, ConfigLoader.LoadFrom(Yaml).RulesVersion);

    [Theory]
    [InlineData("/repo/src/Core/Engine.cs", "strict")]
    [InlineData("/repo/src/Other/Thing.cs", "default")]
    public void ResolvesProfileByGlob(string path, string expected) =>
        Assert.Equal(expected, ProfileResolver.Resolve(ConfigLoader.LoadFrom(Yaml), path));

    [Fact]
    public void UnmatchedPathFallsBackToDefaultProfile() =>
        Assert.Equal("default", ProfileResolver.Resolve(ConfigLoader.LoadFrom("clones:\n  minStatements: 8"), "/x/y.cs"));

    // Demonstrated harm this guards against: renaming a binding's profile "consensus" ->
    // "consenus" must never fall back to `default` while claiming the typo'd name was applied -
    // it must refuse to produce a config at all, naming both the bad profile and the binding
    // that referenced it.
    [Fact]
    public void BindingNamingAnUndeclaredProfileThrowsNamingTheProfileAndTheGlob()
    {
        var exception = Assert.Throws<ConfigurationException>(() => ConfigLoader.LoadFrom("""
            bindings:
              - glob: "**/EVM.Core"
                profile: consenus
            profiles:
              consensus:
                maxMethodLines: 35
            """));

        Assert.Contains("consenus", exception.Message);
        Assert.Contains("**/EVM.Core", exception.Message);
    }

    [Fact]
    public void BindingsThatAllResolveLoadNormally()
    {
        var config = ConfigLoader.LoadFrom(Yaml);

        Assert.Equal("strict", config.Bindings.Single().Profile);
        Assert.Equal(20, config.Profiles["strict"].MaxMethodLines);
    }

    [Fact]
    public void LoadFromDoesNotSetASourcePath() =>
        Assert.Null(ConfigLoader.LoadFrom(Yaml).SourcePath);

    [Fact]
    public void DiscoverRecordsThePathOfTheRulesetItFound()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cq-discover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var rulesetPath = Path.Combine(directory, ConfigLoader.FileName);
        try
        {
            File.WriteAllText(rulesetPath, "clones:\n  minStatements: 8\n");

            var config = ConfigLoader.Discover(directory);

            Assert.Equal(rulesetPath, config.SourcePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscoverLeavesSourcePathNullWhenNoRulesetIsFound()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cq-nodiscover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Null(ConfigLoader.Discover(directory).SourcePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
