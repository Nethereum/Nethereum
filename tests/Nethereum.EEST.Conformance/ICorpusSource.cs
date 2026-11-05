namespace Nethereum.EEST.Conformance
{
    public interface ICorpusSource
    {
        CaseKind Kind { get; }
        string Root { get; }
        bool IsPresent { get; }
        IReadOnlyList<string> Files { get; }
        IReadOnlyList<ConformanceCase> Cases { get; }
        ConformanceCase Find(string id);
    }
}
