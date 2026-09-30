namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class BlockAccessListAccount : TableRow
    {
        public long BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public string Address { get; set; }
        public bool IsCanonical { get; set; } = true;
        public string StorageReads { get; set; }
        public string StorageChanges { get; set; }
        public string BalanceChanges { get; set; }
        public string NonceChanges { get; set; }
        public string CodeChanges { get; set; }
    }
}
