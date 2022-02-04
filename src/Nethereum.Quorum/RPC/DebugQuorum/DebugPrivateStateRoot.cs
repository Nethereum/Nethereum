
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Quorum.RPC.Debug
{
    ///<Summary>
    /// Returns the private state root hash at the specified block number.
    /// 
    /// Parameters
    /// blockNumber: number - integer representing a block number or one of the string tags latest (the last block mined) or pending (the last block mined plus pending transactions).
    /// 
    /// Returns
    /// result: data - private state root hash    
    ///</Summary>
    public interface IDebugPrivateStateRoot
    {
        Task<string> SendRequestAsync(BlockParameter blockNumber, object id = null);
        RpcRequest BuildRequest(BlockParameter blockNumber, object id = null);
    }

    ///<Summary>
/// Returns the private state root hash at the specified block number.
/// 
/// Parameters
/// blockNumber: number - integer representing a block number or one of the string tags latest (the last block mined) or pending (the last block mined plus pending transactions).
/// 
/// Returns
/// result: data - private state root hash    
///</Summary>
    public class DebugPrivateStateRoot : RpcRequestResponseHandler<string>, IDebugPrivateStateRoot
    {
        public DebugPrivateStateRoot(IClient client) : base(client,ApiMethods.debug_privateStateRoot.ToString()) { }

        public Task<string> SendRequestAsync(BlockParameter blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber.GetRPCParamAsNumber());
        }
        public RpcRequest BuildRequest(BlockParameter blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber.GetRPCParamAsNumber());
        }
    }

}

