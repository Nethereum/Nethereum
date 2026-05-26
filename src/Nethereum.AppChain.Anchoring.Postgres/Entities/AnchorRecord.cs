namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class AnchorRecord
    {
        public long Id { get; set; }
        public long ChainId { get; set; }
        public long StartBlock { get; set; }
        public long EndBlock { get; set; }
        public byte ProofSystem { get; set; }
        public byte AnchorVersion { get; set; }
        public byte[] EndBlockHash { get; set; }
        public byte[] PostStateRoot { get; set; }
        public byte[] BlockHashesRoot { get; set; }
        public byte[] ManifestHash { get; set; }
        public byte[] PreviousAnchorHash { get; set; }
        public int ProofBytesLength { get; set; }
        public string TransactionHash { get; set; }
        public long MainchainBlockNumber { get; set; }
        public long Timestamp { get; set; }
        public string OperatorAddress { get; set; }
    }
}
