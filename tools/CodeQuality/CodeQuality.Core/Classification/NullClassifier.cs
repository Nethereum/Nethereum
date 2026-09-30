using CodeQuality.Core.Model;

namespace CodeQuality.Core.Classification;

public sealed class NullClassifier : IClassifier
{
    public Task<Verdict> ClassifyAsync(Finding finding, CancellationToken token) =>
        Task.FromResult(Verdict.Review);
}
