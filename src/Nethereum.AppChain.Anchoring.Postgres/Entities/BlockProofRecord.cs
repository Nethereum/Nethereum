namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class BlockProofRecord
    {
        public long Id { get; set; }
        public long ChainId { get; set; }
        public long BlockNumber { get; set; }
        public byte ProofSystem { get; set; }
        public string ProverAddress { get; set; }
        public string TransactionHash { get; set; }
        public long MainchainBlockNumber { get; set; }
        public long Timestamp { get; set; }
    }
}
