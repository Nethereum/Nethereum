
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Checks if the account initiating the specified transaction has sufficient permissions to execute the transaction.
/// 
/// Parameters
/// txArgs: object - transaction arguments object
/// Returns
/// result: boolean - indicates if transaction is allowed or not    
///</Summary>
    public class QuorumPermissionTransactionAllowed : RpcRequestResponseHandler<bool>
    {
        public QuorumPermissionTransactionAllowed(IClient client) : base(client,ApiMethods.quorumPermission_transactionAllowed.ToString()) { }

        public Task<bool> SendRequestAsync(JObject transactionArguments, object id = null)
        {
            return base.SendRequestAsync(id, transactionArguments);
        }
        public RpcRequest BuildRequest(JObject transactionArguments, object id = null)
        {
            return base.BuildRequest(id, transactionArguments);
        }
    }

}

