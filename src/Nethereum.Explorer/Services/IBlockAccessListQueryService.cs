using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Explorer.Services;

public interface IBlockAccessListQueryService
{
    Task<List<AccountAccess>?> GetForBlockAsync(long blockNumber);
}
