using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockAccessList
    {
        Task<List<AccountAccess>> SendRequestAsync(BlockParameter block, object id = null);
        Task<List<AccountAccess>> SendRequestAsync(string blockHash, object id = null);
        BlockParameter DefaultBlock { get; set; }
    }
}
