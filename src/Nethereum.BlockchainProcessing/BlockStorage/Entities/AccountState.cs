namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class AccountState : TableRow
    {
        public string Address { get; set; }
        public string Balance { get; set; }
        public long Nonce { get; set; }
        public bool IsContract { get; set; }
        public long LastUpdatedBlock { get; set; }
    }
}
