namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class AddressTransaction: TableRow, IAddressTransactionView
    {
        public string BlockNumber { get; set; }
        public string Hash { get; set; }
        public string Address  { get; set; }
    }
}