using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;

namespace CodeQuality.Core.Analysis;

public sealed class StructureAnalyser : IAnalyser
{
    public string Id => "structure";

    public IReadOnlyList<Finding> Analyse(PackageModel model, RuleContext context) =>
        MethodFindings(model, context).Concat(TypeFindings(model, context)).ToList();

    static IEnumerable<Finding> MethodFindings(PackageModel model, RuleContext context) =>
        model.Files
            .SelectMany(file => file.Methods.SelectMany(method =>
                StructureRules.Apply(context with { RelativePath = file.RelativePath }, method)));

    // A type finding is filed against its PRIMARY declaration, not against every file that
    // declares part of it: one type is one unit of work, and one finding per partial file would
    // both triple-count the type and make the split look like progress.
    static IEnumerable<Finding> TypeFindings(PackageModel model, RuleContext context) =>
        model.Types.SelectMany(type =>
            GodClassRule.Apply(context with { RelativePath = type.PrimaryFilePath }, type));
}
