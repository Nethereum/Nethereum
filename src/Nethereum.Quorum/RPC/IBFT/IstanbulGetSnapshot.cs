
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{
    ///<Summary>
    /// Retrieves the state snapshot at the specified block number.
    /// 
    /// Parameters
    /// blockNumber: number or string - (optional) integer representing a block number or the string tag latest (the last block mined); defaults to latest
    /// 
    /// Returns
    /// result: object - snapshot object    
    ///</Summary>
    public interface IIstanbulGetSnapshot
    {
        Task<JObject> SendRequestAsync(BlockParameter blockNumber, object id = null);
        RpcRequest BuildRequest(BlockParameter blockNumber, object id = null);
    }

    ///<Summary>
/// Retrieves the state snapshot at the specified block number.
/// 
/// Parameters
/// blockNumber: number or string - (optional) integer representing a block number or the string tag latest (the last block mined); defaults to latest
/// 
/// Returns
/// result: object - snapshot object    
///</Summary>
    public class IstanbulGetSnapshot : RpcRequestResponseHandler<JObject>, IIstanbulGetSnapshot
    {
        public IstanbulGetSnapshot(IClient client) : base(client,ApiMethods.istanbul_getSnapshot.ToString()) { }

        public Task<JObject> SendRequestAsync(BlockParameter blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber.GetRPCParamAsNumber());
        }
        public RpcRequest BuildRequest(BlockParameter blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber.GetRPCParamAsNumber());
        }
    }

}

