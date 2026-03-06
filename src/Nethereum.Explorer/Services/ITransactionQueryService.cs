using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface ITransactionQueryService
{
    Task<ITransactionView?> GetTransactionAsync(string txHash);
    Task<List<ITransactionView>> GetLatestTransactionsAsync(int count);
    Task<List<ITransactionView>> GetLatestTransactionsPagedAsync(int page, int pageSize);
    Task<List<ITransactionView>> GetBlockTransactionsAsync(long blockNumber);
    Task<List<ITransactionView>> GetAddressTransactionsAsync(string address, int page, int pageSize, string? direction = null);
    Task<int> GetAddressTransactionCountAsync(string address, string? direction = null);
    Task<int> GetTotalTransactionCountAsync();
}
