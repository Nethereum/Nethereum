namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface INFTInventoryView
    {
        string Address { get; }
        string ContractAddress { get; }
        string TokenId { get; }
        string Amount { get; }
        string TokenType { get; }
        long LastUpdatedBlockNumber { get; }
    }
}
