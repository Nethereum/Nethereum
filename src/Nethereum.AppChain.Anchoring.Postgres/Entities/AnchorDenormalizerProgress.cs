namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class AnchorDenormalizerProgress
    {
        public int Id { get; set; } = 1;
        public long LastProcessedAnchorId { get; set; }
    }
}
