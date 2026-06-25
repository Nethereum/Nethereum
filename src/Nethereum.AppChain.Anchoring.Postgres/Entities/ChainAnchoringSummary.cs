namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class ChainAnchoringSummary
    {
        public long ChainId { get; set; }
        public long LatestAnchoredBlock { get; set; }
        public long TotalAnchors { get; set; }
        public long TotalProvenBlocks { get; set; }
        public byte CurrentProofSystem { get; set; }
        public long LastAnchorTimestamp { get; set; }
        public double AverageAnchorIntervalSeconds { get; set; }
        public int ConsecutiveFailures { get; set; }
        public long LastUpdated { get; set; }
    }
}
