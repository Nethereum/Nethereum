
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Returns the unencrypted payload from Tessera.
    /// 
    /// Parameters
    /// id: string - the generated SHA3-512 hash of the encrypted payload from the Private Transaction Manager, in hex (This is seen in the transaction as the input field.)
    /// 
    /// Returns
    /// result: string - unencrypted transaction payload in hex format    
    ///</Summary>
    public interface IEthGetQuorumPayload
    {
        Task<string> SendRequestAsync(string idHash, object id = null);
        RpcRequest BuildRequest(string idHash, object id = null);
    }

    ///<Summary>
/// Returns the unencrypted payload from Tessera.
/// 
/// Parameters
/// id: string - the generated SHA3-512 hash of the encrypted payload from the Private Transaction Manager, in hex (This is seen in the transaction as the input field.)
/// 
/// Returns
/// result: string - unencrypted transaction payload in hex format    
///</Summary>
    public class EthGetQuorumPayload : RpcRequestResponseHandler<string>, IEthGetQuorumPayload
    {
        public EthGetQuorumPayload(IClient client) : base(client,ApiMethods.eth_getQuorumPayload.ToString()) { }

        public Task<string> SendRequestAsync(string idHash, object id = null)
        {
            return base.SendRequestAsync(id, idHash);
        }
        public RpcRequest BuildRequest(string idHash, object id = null)
        {
            return base.BuildRequest(id, idHash);
        }
    }

}

