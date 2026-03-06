namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class AddressTransaction: TableRow, IAddressTransactionView
    {
        public long BlockNumber { get; set; }
        public string Hash { get; set; }
        public string Address  { get; set; }
    }
}