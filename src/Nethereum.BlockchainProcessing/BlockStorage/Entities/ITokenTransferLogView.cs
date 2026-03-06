namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface ITokenTransferLogView
    {
        string TransactionHash { get; }
        long LogIndex { get; }
        long BlockNumber { get; }
        string BlockHash { get; }
        string ContractAddress { get; }
        string EventHash { get; }
        string FromAddress { get; }
        string ToAddress { get; }
        string Amount { get; }
        string TokenId { get; }
        string OperatorAddress { get; }
        string TokenType { get; }
        bool IsCanonical { get; }
    }
}
