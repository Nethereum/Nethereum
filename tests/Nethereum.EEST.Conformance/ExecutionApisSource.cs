namespace Nethereum.EEST.Conformance
{
    public sealed class ExecutionApisSource : CorpusSource
    {
        public ExecutionApisSource(string testsRoot) : base(CaseKind.RpcCompat, testsRoot, "*.io")
        {
        }

        public override bool IsPresent => System.IO.File.Exists(Path.Combine(Root, "chain.rlp"));

        protected override IEnumerable<ConformanceCase> ReadCases(string file)
        {
            var id = RelativeId(file);
            yield return new ConformanceCase(Kind, id, file, id);
        }
    }
}
