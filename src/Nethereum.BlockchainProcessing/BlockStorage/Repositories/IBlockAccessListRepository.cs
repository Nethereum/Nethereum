using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IBlockAccessListRepository
    {
        Task UpsertAsync(AccountAccess account, long blockNumber, string blockHash);
        Task<IReadOnlyList<AccountAccess>> GetForBlockAsync(long blockNumber);
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
