using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using CodeQuality.Core.Rules;

namespace CodeQuality.Core.Analysis;

public sealed class CloneAnalyser : IAnalyser
{
    readonly int _minStatements;

    public CloneAnalyser(int minStatements) => _minStatements = minStatements;

    public string Id => "clones";

    public IReadOnlyList<CloneGroup> LastGroups { get; private set; } = Array.Empty<CloneGroup>();

    public IReadOnlyList<Finding> Analyse(PackageModel model, RuleContext context)
    {
        var hashed = model.Files
            .SelectMany(file => file.Methods
                .Select(method => (Hash: CloneHasher.Hash(file.Root, method, _minStatements), Method: method)))
            .Where(x => x.Hash is not null)
            .Select(x => (x.Hash!, x.Method))
            .ToList();

        LastGroups = CloneHasher.Group(hashed);

        return LastGroups.SelectMany(group => CloneRule.Apply(context, group)).ToList();
    }
}
