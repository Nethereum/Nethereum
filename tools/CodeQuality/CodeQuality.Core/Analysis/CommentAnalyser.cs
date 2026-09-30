using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;

namespace CodeQuality.Core.Analysis;

public sealed class CommentAnalyser : IAnalyser
{
    public string Id => "comments";

    public IReadOnlyList<Finding> Analyse(PackageModel model, RuleContext context) =>
        model.Files
            .SelectMany(file => file.Comments.SelectMany(block =>
                ApplyAll(context with { RelativePath = file.RelativePath }, block)))
            .ToList();

    static IEnumerable<Finding> ApplyAll(RuleContext context, CommentBlock block) =>
        ReferenceRules.Apply(context, block)
            .Concat(CommentedOutCodeRule.Apply(context, block))
            .Concat(TaggedCommentRule.Apply(context, block));
}
