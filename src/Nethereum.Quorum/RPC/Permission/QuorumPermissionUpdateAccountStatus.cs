
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Updates the status of the specified account. This method can be called by an organization admin account.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the account belongs
/// 
/// acctId: string - account ID
/// 
/// action: number -
/// 
/// 1 - for suspending the account
/// 
/// 2 - for activating the suspended account
/// 
/// 3 - for denylisting (blacklisting) the account
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionUpdateAccountStatus : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionUpdateAccountStatus(IClient client) : base(client,ApiMethods.quorumPermission_updateAccountStatus.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string acctId, int action, object id = null)
        {
            return base.SendRequestAsync(id, orgId, acctId, action);
        }
        public RpcRequest BuildRequest(string orgId, string acctId, int action, object id = null)
        {
            return base.BuildRequest(id, orgId, acctId, action);
        }
    }

}

