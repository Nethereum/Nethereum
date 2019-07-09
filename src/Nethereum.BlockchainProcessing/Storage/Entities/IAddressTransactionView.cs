namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface IAddressTransactionView
    {
        string BlockNumber { get;}
        string Hash { get;}
        string Address  { get; }
    }
}
