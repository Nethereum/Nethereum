namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class ChainState : TableRow
    {
        public long? LastCanonicalBlockNumber { get; set; }
        public string LastCanonicalBlockHash { get; set; }
        public long? FinalizedBlockNumber { get; set; }
        public int? ChainId { get; set; }
    }
}
