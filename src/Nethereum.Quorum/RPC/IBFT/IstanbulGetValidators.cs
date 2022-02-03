
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Retrieves the list of authorized validators at the specified block number.
/// 
/// Parameters
/// blockNumber: number or string - (optional) integer representing a block number or the string tag latest (the last block mined); defaults to latest
/// 
/// Returns
/// result: array of strings - list of validator addresses    
///</Summary>
    public class IstanbulGetValidators : RpcRequestResponseHandler<string[]>
    {
        public IstanbulGetValidators(IClient client) : base(client,ApiMethods.istanbul_getValidators.ToString()) { }

        public Task<string[]> SendRequestAsync(BlockParameter blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber.GetRPCParamAsNumber());
        }
        public RpcRequest BuildRequest(BlockParameter blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber.GetRPCParamAsNumber());
        }
    }

}

