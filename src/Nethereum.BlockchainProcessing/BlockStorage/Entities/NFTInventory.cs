namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class NFTInventory : TableRow, INFTInventoryView
    {
        public string Address { get; set; }
        public string ContractAddress { get; set; }
        public string TokenId { get; set; }
        public string Amount { get; set; }
        public string TokenType { get; set; }
        public long LastUpdatedBlockNumber { get; set; }
    }
}
