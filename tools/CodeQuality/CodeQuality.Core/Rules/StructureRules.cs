using CodeQuality.Core.Model;

namespace CodeQuality.Core.Rules;

public static class StructureRules
{
    public static IEnumerable<Finding> Apply(RuleContext context, MethodMetrics method)
    {
        if (method.LineCount > context.Profile.MaxMethodLines)
            yield return Make(context, method, "structure.long-method",
                $"{method.LineCount} lines (limit {context.Profile.MaxMethodLines})");

        if (method.MaxNesting > context.Profile.MaxNesting)
            yield return Make(context, method, "structure.deep-nesting",
                $"nesting depth {method.MaxNesting} (limit {context.Profile.MaxNesting})");

        if (method.ParameterCount > context.Profile.MaxParameters)
            yield return Make(context, method, "structure.many-parameters",
                $"{method.ParameterCount} parameters (limit {context.Profile.MaxParameters})");
    }

    static Finding Make(RuleContext context, MethodMetrics method, string ruleId, string evidence) =>
        new(Package: context.Package,
            FilePath: context.RelativePath,
            Line: method.Line,
            Symbol: $"{method.ContainingType}.{method.Name}",
            Kind: FindingKind.Structure,
            RuleId: ruleId,
            Verdict: Verdict.Review,
            Evidence: evidence,
            Confidence: 1.0,
            Profile: context.ProfileName);
}
