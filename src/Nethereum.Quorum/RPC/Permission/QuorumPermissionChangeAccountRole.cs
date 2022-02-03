
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Assigns a role to the specified account. This method can be called by an organization admin account.
/// 
/// Parameters
/// acctId: string - account ID
/// 
/// orgId: string - organization ID
/// 
/// roleId: string - new role ID to be assigned to the account
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionChangeAccountRole : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionChangeAccountRole(IClient client) : base(client,ApiMethods.quorumPermission_changeAccountRole.ToString()) { }

        public Task<string> SendRequestAsync(string acctId, string orgId, string roleId, object id = null)
        {
            return base.SendRequestAsync(id, acctId, orgId, roleId);
        }
        public RpcRequest BuildRequest(string acctId, string orgId, string roleId, object id = null)
        {
            return base.BuildRequest(id, acctId, orgId, roleId);
        }
    }

}

