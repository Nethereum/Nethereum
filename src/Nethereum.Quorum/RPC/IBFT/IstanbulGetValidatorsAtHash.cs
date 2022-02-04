
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{
    ///<Summary>
    /// Retrieves the list of authorized validators at the specified block hash.
    /// 
    /// Parameters
    /// blockHash: string - block hash
    /// 
    /// Returns
    /// result: array of strings - list of validator addresses    
    ///</Summary>
    public interface IIstanbulGetValidatorsAtHash
    {
        Task<string[]> SendRequestAsync(string blockHash, object id = null);
        RpcRequest BuildRequest(string blockHash, object id = null);
    }

    ///<Summary>
/// Retrieves the list of authorized validators at the specified block hash.
/// 
/// Parameters
/// blockHash: string - block hash
/// 
/// Returns
/// result: array of strings - list of validator addresses    
///</Summary>
    public class IstanbulGetValidatorsAtHash : RpcRequestResponseHandler<string[]>, IIstanbulGetValidatorsAtHash
    {
        public IstanbulGetValidatorsAtHash(IClient client) : base(client,ApiMethods.istanbul_getValidatorsAtHash.ToString()) { }

        public Task<string[]> SendRequestAsync(string blockHash, object id = null)
        {
            return base.SendRequestAsync(id, blockHash);
        }
        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            return base.BuildRequest(id, blockHash);
        }
    }

}

