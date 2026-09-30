using System.Text.Json;
using CodeQuality.Cli;
using CodeQuality.Core.Analysis;
using CodeQuality.Core.Model;
using Xunit;

namespace CodeQuality.Tests.Cli;

public class JsonRendererTests
{
    static TypeDeclarationMetrics Part(string file, int line, int lines, int methods) =>
        new(FilePath: file, Line: line, LineCount: lines, FullName: "Sample.Big", Name: "Big",
            Namespace: "Sample", Kind: "class", IsPartial: true, MethodCount: methods,
            PropertyCount: 0, FieldCount: 0, EventCount: 0, ConstructorCount: 0,
            LongMethodCount: 1);

    static readonly TypeMetrics Big = new(
        FullName: "Sample.Big", Name: "Big", Namespace: "Sample", Kind: "class", IsPartial: true,
        PrimaryFilePath: "Big.cs", PrimaryLine: 3, TotalLines: 900,
        MethodCount: 24, PropertyCount: 0, FieldCount: 0, EventCount: 0, ConstructorCount: 0,
        LongMethodCount: 2, MaxMethodLinesApplied: 35,
        Parts: new[] { Part("Big.cs", 3, 600, 14), Part("Big.Extra.cs", 3, 300, 10) });

    static readonly Finding GodClass = new("Pkg", "Big.cs", 3, "Sample.Big", FindingKind.Structure,
        "structure.god-class", Verdict.Review, "24 members (limit 20)", 1.0, "default");

    static readonly Finding LongMethod = new("Pkg", "Big.cs", 40, "Big.Do", FindingKind.Structure,
        "structure.long-method", Verdict.Review, "60 lines (limit 35)", 1.0, "default");

    static AnalysisResult Result(params Finding[] findings) => new(
        Package: "Pkg", ProfileName: "default", ConfigSourcePath: null, ReferenceChecksInert: true,
        FilesAnalysed: 2, FilesExcluded: 0, FilesUnreadable: 0, FilesWithDisabledRegions: 0,
        MethodCount: 24, CommentBlockCount: 0, Methods: Array.Empty<MethodMetrics>(),
        Findings: findings, Clones: Array.Empty<CloneGroup>(),
        TypeCount: 1, Types: new[] { Big });

    static JsonElement FirstFinding(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("Findings")[0];

    // A class-size finding is the only finding whose subject is not at one file:line. Emitting it
    // in the flat finding shape would tell a machine reader that a type declared across two
    // `partial` files lives in one place - the exact misreading the aggregate exists to prevent -
    // so the per-file breakdown has to travel with the finding, not merely be summarised in it.
    [Fact]
    public void GodClassJsonCarriesThePerFileBreakdownAndNotOnlyTheAggregate()
    {
        var finding = FirstFinding(JsonRenderer.Findings(Result(GodClass), new[] { GodClass }));
        var type = finding.GetProperty("Type");

        Assert.Equal(2, type.GetProperty("DeclaringFileCount").GetInt32());
        Assert.Equal(24, type.GetProperty("MemberCount").GetInt32());
        Assert.Equal(900, type.GetProperty("TotalLines").GetInt32());
        Assert.Equal(35, type.GetProperty("MaxMethodLinesApplied").GetInt32());
        Assert.True(type.GetProperty("IsPartial").GetBoolean());

        var parts = type.GetProperty("Parts").EnumerateArray().ToList();

        Assert.Equal(2, parts.Count);
        Assert.Equal("Big.cs", parts[0].GetProperty("FilePath").GetString());
        Assert.Equal(600, parts[0].GetProperty("LineCount").GetInt32());
        Assert.Equal(14, parts[0].GetProperty("MemberCount").GetInt32());
        Assert.Equal("Big.Extra.cs", parts[1].GetProperty("FilePath").GetString());
        Assert.Equal(300, parts[1].GetProperty("LineCount").GetInt32());
        Assert.Equal(10, parts[1].GetProperty("MemberCount").GetInt32());
    }

    // Non-vacuity twin: an implementation that attached the type block to EVERY finding, or that
    // attached the first type in the package to whatever finding it was handed, would also pass
    // the test above. A per-method finding must carry no type block at all.
    [Fact]
    public void APerMethodFindingCarriesNoTypeBlock()
    {
        var finding = FirstFinding(JsonRenderer.Findings(Result(LongMethod), new[] { LongMethod }));

        Assert.False(finding.TryGetProperty("Type", out _));
        Assert.Equal("structure.long-method", finding.GetProperty("RuleId").GetString());
    }

    // A class-size finding whose symbol matches no measured type is a bug elsewhere, but it must
    // degrade to the flat shape rather than throw and lose the whole report.
    [Fact]
    public void AGodClassFindingWithNoMatchingTypeStillSerialisesWithoutIt()
    {
        var orphan = GodClass with { Symbol = "Sample.Vanished" };
        var finding = FirstFinding(JsonRenderer.Findings(Result(orphan), new[] { orphan }));

        Assert.False(finding.TryGetProperty("Type", out _));
        Assert.Equal("Sample.Vanished", finding.GetProperty("Symbol").GetString());
    }

    [Fact]
    public void CoverageEnvelopeReportsHowManyTypesWereMeasured()
    {
        var coverage = JsonDocument.Parse(JsonRenderer.Findings(Result(), Array.Empty<Finding>()))
            .RootElement.GetProperty("Coverage");

        Assert.Equal(1, coverage.GetProperty("TypeCount").GetInt32());
    }
}
