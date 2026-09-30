using System.Text.RegularExpressions;
using CodeQuality.Core.Model;

namespace CodeQuality.Core.Rules;

public static class TaggedCommentRule
{
    static readonly Regex Tags = new(@"\b(TODO|HACK|FIXME|XXX)\b", RegexOptions.Compiled);

    public static IEnumerable<Finding> Apply(RuleContext context, CommentBlock block)
    {
        if (!Tags.IsMatch(block.Text)) yield break;

        yield return new Finding(
            Package: context.Package,
            FilePath: context.RelativePath,
            Line: block.StartLine,
            Symbol: block.EnclosingMethod ?? "<file>",
            Kind: FindingKind.Comment,
            RuleId: "comment.tagged",
            Verdict: Verdict.Review,
            Evidence: ReferenceRules.Excerpt(block.Text),
            Confidence: 1.0,
            Profile: context.ProfileName);
    }
}
