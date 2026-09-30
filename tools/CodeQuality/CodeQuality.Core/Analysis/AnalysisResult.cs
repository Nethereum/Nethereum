using CodeQuality.Core.Model;

namespace CodeQuality.Core.Analysis;

public sealed record AnalysisResult(
    string Package,
    string ProfileName,
    string? ConfigSourcePath,
    bool ReferenceChecksInert,
    int FilesAnalysed,
    int FilesExcluded,
    int FilesUnreadable,
    int FilesWithDisabledRegions,
    int MethodCount,
    int CommentBlockCount,
    IReadOnlyList<MethodMetrics> Methods,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<CloneGroup> Clones,
    // A type count belongs beside the method count for the same reason the method count is here:
    // without it a report with no class-size findings is indistinguishable from a run in which
    // the type dimension never read anything.
    int TypeCount,
    IReadOnlyList<TypeMetrics> Types);
