using CodeQuality.Cli;
using CodeQuality.Core.Analysis;
using CodeQuality.Core.Model;
using Xunit;

namespace CodeQuality.Tests.Cli;

public class ReportRendererTests
{
    // Ground truth: every line below was hand-assembled from the format strings in
    // ReportRenderer.cs (header, config-source line, reference-inert line, the two coverage
    // warnings, one uppercase heading per non-Docs FindingKind - "none" when its group is empty -
    // structural findings ranked by the measured value parsed out of Evidence rather than by
    // Line, up to 10 findings per non-empty group, and a "... N more not shown" line whenever a
    // group exceeds 10) rather than by running the renderer and pasting its output. The type
    // count was added to the header line by hand from the same format string when the class-size
    // dimension landed - 4 is seeded here rather than 0 so a renderer that dropped the count, or
    // printed the method count twice, still fails. The structure
    // findings are seeded with Evidence values that run in the OPPOSITE order to their Line
    // numbers (line 1 carries the biggest "N lines", line 12 the smallest) specifically so that a
    // renderer still sorting by Line would print a different top-10 than the one asserted here -
    // ranking by Line could not produce this expected output by coincidence.
    [Fact]
    public void SummaryRendersExactlyTheExpectedReportText()
    {
        var structureFindings = Enumerable.Range(1, 12)
            .Select(line => new Finding("Demo", "A.cs", line, "A.Foo", FindingKind.Structure,
                "structure.long-method", Verdict.Review, $"{13 - line} lines (limit 35)", 1.0, "default"))
            .ToList();
        var commentFinding = new Finding("Demo", "B.cs", 99, "B.Bar", FindingKind.Comment,
            "comment.commented-out-code", Verdict.Delete, "excerpt", 1.0, "default");

        var result = new AnalysisResult(
            Package: "Demo",
            ProfileName: "default",
            ConfigSourcePath: "/rules/codequality.yml",
            ReferenceChecksInert: true,
            FilesAnalysed: 3,
            FilesExcluded: 1,
            FilesUnreadable: 1,
            FilesWithDisabledRegions: 1,
            MethodCount: 7,
            CommentBlockCount: 2,
            Methods: Array.Empty<MethodMetrics>(),
            Findings: structureFindings.Append(commentFinding).ToList(),
            Clones: Array.Empty<CloneGroup>(),
            TypeCount: 4,
            Types: Array.Empty<TypeMetrics>());

        var expected = string.Join(Environment.NewLine, new[]
        {
            "Demo    profile: default",
            "config: /rules/codequality.yml",
            "reference checks: inert - no vendor list configured",
            "3 files, 4 types, 7 methods, 2 comment blocks, 1 generated files excluded",
            "WARNING: 1 file(s) could not be read and were not analysed",
            "WARNING: 1 file(s) contain inactive #if regions not analysed - set parse.preprocessorSymbols to cover them",
            "",
            "STRUCTURE",
            "  Review     12",
            "    A.cs:1  structure.long-method  12 lines (limit 35)",
            "    A.cs:2  structure.long-method  11 lines (limit 35)",
            "    A.cs:3  structure.long-method  10 lines (limit 35)",
            "    A.cs:4  structure.long-method  9 lines (limit 35)",
            "    A.cs:5  structure.long-method  8 lines (limit 35)",
            "    A.cs:6  structure.long-method  7 lines (limit 35)",
            "    A.cs:7  structure.long-method  6 lines (limit 35)",
            "    A.cs:8  structure.long-method  5 lines (limit 35)",
            "    A.cs:9  structure.long-method  4 lines (limit 35)",
            "    A.cs:10  structure.long-method  3 lines (limit 35)",
            "    ... 2 more not shown",
            "",
            "COMMENT",
            "  Delete     1",
            "    B.cs:99  comment.commented-out-code  excerpt",
            "",
            "REFERENCE",
            "  none",
            "",
            "CLONE",
            "  none",
            "",
        }) + Environment.NewLine;

        Assert.Equal(expected, ReportRenderer.Summary(result));
    }

    // Ground truth: hand-assembled from AppendHeader's null-path branch. A run that never found a
    // codequality.yml and never received --config must say so explicitly rather than printing a
    // profile name and leaving the reader to assume a ruleset was applied.
    [Fact]
    public void SummaryStatesDefaultsAreInForceWhenNoConfigWasFound()
    {
        var result = new AnalysisResult(
            Package: "Demo",
            ProfileName: "default",
            ConfigSourcePath: null,
            ReferenceChecksInert: true,
            FilesAnalysed: 0,
            FilesExcluded: 0,
            FilesUnreadable: 0,
            FilesWithDisabledRegions: 0,
            MethodCount: 0,
            CommentBlockCount: 0,
            Methods: Array.Empty<MethodMetrics>(),
            Findings: Array.Empty<Finding>(),
            Clones: Array.Empty<CloneGroup>(),
            TypeCount: 0,
            Types: Array.Empty<TypeMetrics>());

        var report = ReportRenderer.Summary(result);

        Assert.Contains("config: none found - built-in defaults in force" + Environment.NewLine, report);
        Assert.Contains("reference checks: inert - no vendor list configured" + Environment.NewLine, report);
    }

    // Ground truth: a resolved config whose vendor list is non-empty must not print the inert
    // note - printing it unconditionally would be as misleading as never printing it at all.
    [Fact]
    public void SummaryOmitsTheInertNoteWhenReferenceChecksActuallyRan()
    {
        var result = new AnalysisResult(
            Package: "Demo",
            ProfileName: "default",
            ConfigSourcePath: "/rules/codequality.yml",
            ReferenceChecksInert: false,
            FilesAnalysed: 0,
            FilesExcluded: 0,
            FilesUnreadable: 0,
            FilesWithDisabledRegions: 0,
            MethodCount: 0,
            CommentBlockCount: 0,
            Methods: Array.Empty<MethodMetrics>(),
            Findings: Array.Empty<Finding>(),
            Clones: Array.Empty<CloneGroup>(),
            TypeCount: 0,
            Types: Array.Empty<TypeMetrics>());

        Assert.DoesNotContain("inert", ReportRenderer.Summary(result));
    }
}
