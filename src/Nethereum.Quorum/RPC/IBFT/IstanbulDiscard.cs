
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.IBFT
{

///<Summary>
/// Drops a currently running candidate, stopping further votes from being cast either for or against the candidate.
/// 
/// Parameters
/// address: string - address of the candidate
/// 
/// Returns
/// result: null    
///</Summary>
    public class IstanbulDiscard : RpcRequestResponseHandler<string>
    {
        public IstanbulDiscard(IClient client) : base(client,ApiMethods.istanbul_discard.ToString()) { }

        public Task<string> SendRequestAsync(string address, object id = null)
        {
            return base.SendRequestAsync(id, address);
        }
        public RpcRequest BuildRequest(string address, object id = null)
        {
            return base.BuildRequest(id, address);
        }
    }

}

