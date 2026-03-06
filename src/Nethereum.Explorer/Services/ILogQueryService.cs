using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface ILogQueryService
{
    Task<List<ITransactionLogView>> GetTransactionLogsAsync(string txHash);
    Task<List<ITransactionLogView>> GetContractLogsAsync(string contractAddress, int page, int pageSize);
    Task<List<string>> GetTokenAddressesForAccountAsync(string accountAddress);
    Task<List<NftTransferRecord>> GetNftTransfersForAccountAsync(string accountAddress);
}
