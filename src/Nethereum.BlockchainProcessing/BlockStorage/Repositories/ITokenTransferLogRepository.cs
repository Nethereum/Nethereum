using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITokenTransferLogRepository
    {
        Task UpsertAsync(TokenTransferLog log);
        Task<ITokenTransferLogView> FindByTransactionHashAndLogIndexAsync(string hash, BigInteger logIndex);
        Task<IEnumerable<ITokenTransferLogView>> GetByAddressAsync(string address, int page, int pageSize);
        Task<IEnumerable<ITokenTransferLogView>> GetByContractAsync(string contractAddress, int page, int pageSize);
        Task<IEnumerable<ITokenTransferLogView>> GetByBlockNumberAsync(long blockNumber);
    }
}
