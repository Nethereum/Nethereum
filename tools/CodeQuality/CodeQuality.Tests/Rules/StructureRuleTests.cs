using CodeQuality.Core.Config;
using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;
using Xunit;

namespace CodeQuality.Tests.Rules;

public class StructureRuleTests
{
    static RuleContext Context(Profile? profile = null) => new(
        Package: "Pkg",
        RelativePath: "src/Pkg/Thing.cs",
        ProfileName: "default",
        Profile: profile ?? new Profile(),
        Config: ConfigLoader.LoadFrom(string.Empty));

    static MethodMetrics Method(int lines = 10, int nesting = 1, int parameters = 2) => new(
        FilePath: "src/Pkg/Thing.cs", Line: 100, ContainingType: "Thing", Name: "Do",
        Signature: "Do(int, int)", LineCount: lines, StatementCount: lines,
        MaxNesting: nesting, ParameterCount: parameters, IsPublic: true);

    [Fact]
    public void SmallMethodProducesNoFinding() =>
        Assert.Empty(StructureRules.Apply(Context(), Method()));

    [Fact]
    public void LongMethodIsFlaggedForReview()
    {
        var finding = Assert.Single(StructureRules.Apply(Context(), Method(lines: 702)));

        Assert.Equal("structure.long-method", finding.RuleId);
        Assert.Equal(Verdict.Review, finding.Verdict);
        Assert.Equal(FindingKind.Structure, finding.Kind);
        Assert.Contains("702", finding.Evidence);
        Assert.Equal(1.0, finding.Confidence);
    }

    [Fact]
    public void DeepNestingIsFlagged()
    {
        var finding = Assert.Single(StructureRules.Apply(Context(), Method(nesting: 6)));
        Assert.Equal("structure.deep-nesting", finding.RuleId);
    }

    [Fact]
    public void TooManyParametersIsFlagged()
    {
        var finding = Assert.Single(StructureRules.Apply(Context(), Method(parameters: 9)));
        Assert.Equal("structure.many-parameters", finding.RuleId);
    }

    [Fact]
    public void ThresholdsComeFromTheProfile()
    {
        var relaxed = new Profile { MaxMethodLines = 1000 };
        Assert.Empty(StructureRules.Apply(Context(relaxed), Method(lines: 702)));
    }

    [Fact]
    public void FindingCarriesSymbolAndProfile()
    {
        var finding = Assert.Single(StructureRules.Apply(Context(), Method(lines: 702)));

        Assert.Equal("Thing.Do", finding.Symbol);
        Assert.Equal("default", finding.Profile);
        Assert.Equal("src/Pkg/Thing.cs", finding.FilePath);
        Assert.Equal(100, finding.Line);
    }
}
