using CodeQuality.Core.Model;

namespace CodeQuality.Core.Rules;

// The per-method rules rank a single method against a limit. A ten-line method is easy to read,
// so a tree can be entirely free of long-method findings and still be unmaintainable: the cost
// concentrates in a handful of types that do too many things, and ranking by METHOD hides that
// because a type's findings are scattered across the list one method at a time.
//
// This rule ranks the TYPE. It deliberately does NOT trigger on aggregate line count, because
// lines are the one number the recommended remedy - split into `partial` files, then into real
// classes - can move without changing anything real. Lines are still reported, as scale.
public static class GodClassRule
{
    public const string RuleId = "structure.god-class";

    public static IEnumerable<Finding> Apply(RuleContext context, TypeMetrics type)
    {
        var reasons = Reasons(context, type).ToList();
        if (reasons.Count == 0) yield break;

        yield return new Finding(
            Package: context.Package,
            FilePath: context.RelativePath,
            Line: type.PrimaryLine,
            Symbol: type.FullName,
            Kind: FindingKind.Structure,
            RuleId: RuleId,
            Verdict: Verdict.Review,
            Evidence: string.Join("; ", reasons.Append(Scale(type))),
            Confidence: 1.0,
            Profile: context.ProfileName);
    }

    // The triggering numbers come first and the scale note last, so the first run of digits in
    // the evidence string is always a measured value that exceeded its limit - which is what the
    // report renderer ranks structural findings on.
    static IEnumerable<string> Reasons(RuleContext context, TypeMetrics type)
    {
        if (type.MemberCount > context.Profile.MaxTypeMembers)
            yield return $"{type.MemberCount} members (limit {context.Profile.MaxTypeMembers})";

        if (type.LongMethodCount > context.Profile.MaxLongMethodsPerType)
            yield return $"{type.LongMethodCount} methods over {type.MaxMethodLinesApplied} lines "
                         + $"(limit {context.Profile.MaxLongMethodsPerType})";
    }

    // For a type split across files the breakdown is named inline rather than left to the JSON
    // renderer alone: a reader of the text report has to be able to see that the aggregate came
    // from several files, or the split reads as if the type were that size in one place.
    static string Scale(TypeMetrics type)
    {
        var files = type.DeclaringFileCount;
        var scale = $"{type.TotalLines} lines across {files} file{(files == 1 ? "" : "s")}";

        if (files == 1) return scale;

        var breakdown = type.Parts
            .GroupBy(p => p.FilePath, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}:{g.Sum(p => p.LineCount)}");

        return $"{scale} ({string.Join(", ", breakdown)})";
    }
}
