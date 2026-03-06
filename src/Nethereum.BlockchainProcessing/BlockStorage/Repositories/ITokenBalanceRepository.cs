using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITokenBalanceRepository
    {
        Task UpsertAsync(TokenBalance balance);
        Task UpsertBatchAsync(IEnumerable<TokenBalance> balances);
        Task<IEnumerable<ITokenBalanceView>> GetByAddressAsync(string address);
        Task<IEnumerable<ITokenBalanceView>> GetByContractAsync(string contractAddress, int page, int pageSize);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
