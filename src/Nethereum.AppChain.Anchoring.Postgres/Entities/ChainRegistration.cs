namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class ChainRegistration
    {
        public long Id { get; set; }
        public long ChainId { get; set; }
        public byte[] GenesisHash { get; set; }
        public byte MinimumProofSystem { get; set; }
        public byte MinimumAnchorVersion { get; set; }
        public string AuthorityAddress { get; set; }
        public string TransactionHash { get; set; }
        public long BlockNumber { get; set; }
        public long Timestamp { get; set; }
    }
}
