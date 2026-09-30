using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth
{
    public interface IEthSimulateV1
    {
        BlockParameter DefaultBlock { get; set; }

        RpcRequest BuildRequest(EthSimulateInput input, BlockParameter block, object id = null);
        Task<List<EthSimulateBlockResult>> SendRequestAsync(EthSimulateInput input, object id = null);
        Task<List<EthSimulateBlockResult>> SendRequestAsync(EthSimulateInput input, BlockParameter block, object id = null);
    }
}
