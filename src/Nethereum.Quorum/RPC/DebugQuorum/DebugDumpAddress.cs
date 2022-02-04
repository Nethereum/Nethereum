
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Debug
{
    ///<Summary>
    /// Retrieves the state of an address at the specified block number.
    /// 
    /// Parameters
    /// address: string - account address of the state to retrieve
    /// 
    /// blockNumber: number - integer representing a block number or one of the string tags latest (the last block mined) or pending (the last block mined plus pending transactions)
    /// 
    /// Returns
    /// result: object - state of the account address    
    ///</Summary>
    public interface IDebugDumpAddress
    {
        Task<JObject> SendRequestAsync(string address, BlockParameter blockNumber, object id = null);
        RpcRequest BuildRequest(string address, BlockParameter blockNumber, object id = null);
    }

    ///<Summary>
/// Retrieves the state of an address at the specified block number.
/// 
/// Parameters
/// address: string - account address of the state to retrieve
/// 
/// blockNumber: number - integer representing a block number or one of the string tags latest (the last block mined) or pending (the last block mined plus pending transactions)
/// 
/// Returns
/// result: object - state of the account address    
///</Summary>
    public class DebugDumpAddress : RpcRequestResponseHandler<JObject>, IDebugDumpAddress
    {
        public DebugDumpAddress(IClient client) : base(client,ApiMethods.debug_dumpAddress.ToString()) { }

        public Task<JObject> SendRequestAsync(string address, BlockParameter blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, address, blockNumber.GetRPCParamAsNumber());
        }
        public RpcRequest BuildRequest(string address, BlockParameter blockNumber, object id = null)
        {
            return base.BuildRequest(id, address, blockNumber.GetRPCParamAsNumber());
        }
    }

}

