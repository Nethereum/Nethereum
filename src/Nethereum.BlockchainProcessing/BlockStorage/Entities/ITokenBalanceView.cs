namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface ITokenBalanceView
    {
        string Address { get; }
        string ContractAddress { get; }
        string Balance { get; }
        string TokenType { get; }
        long LastUpdatedBlockNumber { get; }
    }
}
