using CodeQuality.Core.Model;

namespace CodeQuality.Core.Classification;

public interface IClassifier
{
    Task<Verdict> ClassifyAsync(Finding finding, CancellationToken token);
}
