namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface ITokenMetadataView
    {
        string ContractAddress { get; }
        string Name { get; }
        string Symbol { get; }
        int Decimals { get; }
        string TokenType { get; }
    }
}
