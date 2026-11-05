namespace Nethereum.AppChain.Anchoring.Postgres
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DataIndexing, "appchain-anchoring-postgres", "Denormalizer options")]
    public sealed class AnchorSummaryDenormalizerOptions
    {
        public int ProcessingIntervalSeconds { get; set; } = 10;
        public int BatchSize { get; set; } = 500;
    }
}
