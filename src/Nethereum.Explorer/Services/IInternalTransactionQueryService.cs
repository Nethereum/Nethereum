using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface IInternalTransactionQueryService
{
    Task<List<IInternalTransactionView>> GetInternalTransactionsByTxHashAsync(string txHash);
    Task<List<IInternalTransactionView>> GetInternalTransactionsForAddressAsync(string address, int page, int pageSize, string? direction = null);
    Task<int> GetAddressInternalTransactionCountAsync(string address, string? direction = null);
}
