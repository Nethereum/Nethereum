
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Removes the specified role from an organization. This method can be called by an organization admin account.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the role belongs
/// 
/// roleId: string - role ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionRemoveRole : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionRemoveRole(IClient client) : base(client,ApiMethods.quorumPermission_removeRole.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string roleId, object id = null)
        {
            return base.SendRequestAsync(id, orgId, roleId);
        }
        public RpcRequest BuildRequest(string orgId, string roleId, object id = null)
        {
            return base.BuildRequest(id, orgId, roleId);
        }
    }

}

