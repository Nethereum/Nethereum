
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{
    ///<Summary>
    /// Retrieves the state snapshot at the specified block hash.
    /// 
    /// Parameters
    /// blockHash: string - block hash
    /// 
    /// Returns
    /// result: object - snapshot object    
    ///</Summary>
    public interface IIstanbulGetSnapshotAtHash
    {
        Task<JObject> SendRequestAsync(string blockHash, object id = null);
        RpcRequest BuildRequest(string blockHash, object id = null);
    }

    ///<Summary>
/// Retrieves the state snapshot at the specified block hash.
/// 
/// Parameters
/// blockHash: string - block hash
/// 
/// Returns
/// result: object - snapshot object    
///</Summary>
    public class IstanbulGetSnapshotAtHash : RpcRequestResponseHandler<JObject>, IIstanbulGetSnapshotAtHash
    {
        public IstanbulGetSnapshotAtHash(IClient client) : base(client,ApiMethods.istanbul_getSnapshotAtHash.ToString()) { }

        public Task<JObject> SendRequestAsync(string blockHash, object id = null)
        {
            return base.SendRequestAsync(id, blockHash);
        }
        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            return base.BuildRequest(id, blockHash);
        }
    }

}

