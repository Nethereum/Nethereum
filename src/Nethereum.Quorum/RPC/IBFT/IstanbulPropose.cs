
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{
    ///<Summary>
    /// Injects a new authorization candidate that the validator attempts to push through. If a majority of the validators vote the candidate in/out, the candidate is added/removed in the validator set.
    /// 
    /// Parameters
    /// address: string - address of candidate
    /// 
    /// auth: boolean - true votes the candidate in and false votes out
    /// 
    /// Returns
    /// result: null    
    ///</Summary>
    public interface IIstanbulPropose
    {
        Task<string> SendRequestAsync(string address, bool auth, object id = null);
        RpcRequest BuildRequest(string address, bool auth, object id = null);
    }

    ///<Summary>
/// Injects a new authorization candidate that the validator attempts to push through. If a majority of the validators vote the candidate in/out, the candidate is added/removed in the validator set.
/// 
/// Parameters
/// address: string - address of candidate
/// 
/// auth: boolean - true votes the candidate in and false votes out
/// 
/// Returns
/// result: null    
///</Summary>
    public class IstanbulPropose : RpcRequestResponseHandler<string>, IIstanbulPropose
    {
        public IstanbulPropose(IClient client) : base(client,ApiMethods.istanbul_propose.ToString()) { }

        public Task<string> SendRequestAsync(string address, bool auth, object id = null)
        {
            return base.SendRequestAsync(id, address, auth);
        }
        public RpcRequest BuildRequest(string address, bool auth, object id = null)
        {
            return base.BuildRequest(id, address, auth);
        }
    }

}

