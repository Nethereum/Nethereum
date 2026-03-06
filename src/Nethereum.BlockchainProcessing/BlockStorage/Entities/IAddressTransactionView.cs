namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface IAddressTransactionView
    {
        long BlockNumber { get;}
        string Hash { get;}
        string Address  { get; }
    }
}
