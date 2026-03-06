namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public interface IInternalTransactionView
    {
        string TransactionHash { get; }
        long BlockNumber { get; }
        int TraceIndex { get; }
        int Depth { get; }
        string Type { get; }
        string AddressFrom { get; }
        string AddressTo { get; }
        string Value { get; }
        string Gas { get; }
        string GasUsed { get; }
        string Input { get; }
        string Output { get; }
        string Error { get; }
        string RevertReason { get; }
        string BlockHash { get; }
        bool IsCanonical { get; }
    }
}
