using CodeQuality.Core.Model;

namespace CodeQuality.Core.Rules;

public static class CloneRule
{
    public static IEnumerable<Finding> Apply(RuleContext context, CloneGroup group) =>
        group.Members.Select(member => new Finding(
            Package: context.Package,
            FilePath: member.FilePath,
            Line: member.Line,
            Symbol: $"{member.ContainingType}.{member.Name}",
            Kind: FindingKind.Clone,
            RuleId: "clone.duplicate-method",
            Verdict: Verdict.Review,
            Evidence: $"{group.Members.Count} copies, {group.StatementCount} statements: "
                      + string.Join(", ", group.Members.Select(m => $"{Path.GetFileName(m.FilePath)}:{m.Line}")),
            Confidence: 1.0,
            Profile: context.ProfileName));
}
