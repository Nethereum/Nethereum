using System.Text;
using CodeQuality.Core.Analysis;
using CodeQuality.Core.Model;

namespace CodeQuality.Cli;

public static class ReportRenderer
{
    public static string Summary(AnalysisResult result)
    {
        var report = new StringBuilder();
        AppendHeader(report, result);
        AppendCoverageWarnings(report, result);
        report.AppendLine();
        AppendFindingsByKind(report, result);

        return report.ToString();
    }

    static void AppendHeader(StringBuilder report, AnalysisResult result)
    {
        report.AppendLine($"{result.Package}    profile: {result.ProfileName}");
        report.AppendLine(result.ConfigSourcePath is { } configPath
            ? $"config: {configPath}"
            : "config: none found - built-in defaults in force");

        // Vendors is what every reference finding is gated on (ReferenceRules.Classify returns
        // None outright when it is empty), so an empty list means the dimension never had a
        // chance to produce a finding - indistinguishable, without this line, from "ran and found
        // nothing to flag".
        if (result.ReferenceChecksInert)
            report.AppendLine("reference checks: inert - no vendor list configured");

        // The type count sits beside the method count so a report carrying no class-size finding
        // can be read as "measured N types, none oversized" rather than "the type dimension may
        // never have run".
        report.AppendLine($"{result.FilesAnalysed} files, {result.TypeCount} types, "
                          + $"{result.MethodCount} methods, "
                          + $"{result.CommentBlockCount} comment blocks, "
                          + $"{result.FilesExcluded} generated files excluded");
    }

    static void AppendCoverageWarnings(StringBuilder report, AnalysisResult result)
    {
        // Ruling P28: an unreadable file is a FAILURE to analyse, not a decision not to. Folding it
        // into the excluded count would let a locked or malformed file read as deliberate omission.
        if (result.FilesUnreadable > 0)
            report.AppendLine($"WARNING: {result.FilesUnreadable} file(s) could not be read and were "
                              + "not analysed");

        // Ruling P16: code inside an inactive `#if` was never read, so a clean report over such a
        // file means "not analysed", not "no findings". Say so rather than implying coverage.
        if (result.FilesWithDisabledRegions > 0)
            report.AppendLine($"WARNING: {result.FilesWithDisabledRegions} file(s) contain inactive "
                              + "#if regions not analysed - set parse.preprocessorSymbols to cover them");
    }

    // Docs never runs (deliberately deferred - see FindingKind.Docs), so it gets no heading at
    // all, not even "none": printing one would claim a dimension ran when the tool has no analyser
    // for it. Every other kind is produced by a built-in analyser on every run, so a heading is
    // owed to it whether or not it found anything - the reader must be able to tell "ran, found
    // nothing" apart from "never ran".
    static readonly FindingKind[] KindsThatAlwaysRun =
        Enum.GetValues<FindingKind>().Where(k => k != FindingKind.Docs).ToArray();

    static void AppendFindingsByKind(StringBuilder report, AnalysisResult result)
    {
        foreach (var kind in KindsThatAlwaysRun)
            AppendKindSection(report, kind, result.Findings.Where(f => f.Kind == kind).ToList());
    }

    static void AppendKindSection(StringBuilder report, FindingKind kind, List<Finding> group)
    {
        report.AppendLine(kind.ToString().ToUpperInvariant());

        if (group.Count == 0)
        {
            report.AppendLine("  none");
            report.AppendLine();
            return;
        }

        foreach (var verdict in group.GroupBy(f => f.Verdict).OrderBy(g => g.Key))
            report.AppendLine($"  {verdict.Key,-10} {verdict.Count()}");

        // The ranking IS the work queue: a structural finding's size (the measured value against
        // the profile's limit, e.g. "12 lines" or "nesting depth 4") is what makes one worth fixing
        // before another. Ranking by line number instead buries the worst offenders behind
        // "more not shown" for no reason connected to their severity.
        var ranked = kind == FindingKind.Structure
            ? group.OrderByDescending(f => MeasuredValue(f.Evidence)).ThenByDescending(f => f.Line)
            : group.OrderByDescending(f => f.Line);

        foreach (var finding in ranked.Take(10))
            report.AppendLine($"    {finding.FilePath}:{finding.Line}  {finding.RuleId}  {finding.Evidence}");

        if (group.Count > 10)
            report.AppendLine($"    ... {group.Count - 10} more not shown");

        report.AppendLine();
    }

    // Every structure.* evidence string ("12 lines (limit 35)", "nesting depth 4 (limit 3)",
    // "6 parameters (limit 6)") puts the measured value in the first run of digits and the
    // configured limit in a later one - reading the first integer in the string is therefore
    // always the measured value, never the limit, regardless of which structure rule produced it.
    static int MeasuredValue(string evidence)
    {
        var start = evidence.IndexOfAny("0123456789".ToCharArray());
        if (start < 0) return 0;

        var end = start;
        while (end < evidence.Length && char.IsDigit(evidence[end])) end++;

        return int.Parse(evidence[start..end]);
    }
}
