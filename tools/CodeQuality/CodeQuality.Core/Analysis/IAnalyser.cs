using CodeQuality.Core.Model;
using CodeQuality.Core.Rules;

namespace CodeQuality.Core.Analysis;

public interface IAnalyser
{
    string Id { get; }

    IReadOnlyList<Finding> Analyse(PackageModel model, RuleContext context);
}
