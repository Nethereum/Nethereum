namespace Nethereum.Explorer.Services;

public interface ITransactionTraceService
{
    Task<TransactionTraceResult?> TraceTransactionAsync(string txHash);
    Task<StateDiffResult?> GetStateDiffAsync(string txHash);
}
