namespace Nethereum.AppChain.Anchoring.Postgres
{
    public sealed class AnchorSummaryDenormalizerOptions
    {
        public int ProcessingIntervalSeconds { get; set; } = 10;
        public int BatchSize { get; set; } = 500;
    }
}
