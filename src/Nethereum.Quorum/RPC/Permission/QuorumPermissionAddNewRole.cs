
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Creates a new role for the organization. This method can be called by an organization admin account.
/// 
/// Parameters
/// orgId: string - organization ID for which the role is being created
/// 
/// roleId: string - unique role ID
/// 
/// accountAccess: number - account level access
/// 
/// isVoter: boolean - indicates if the role is a voting role
/// 
/// isAdminRole: boolean - indicates if the role is an admin role    
///</Summary>
    public class QuorumPermissionAddNewRole : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionAddNewRole(IClient client) : base(client,ApiMethods.quorumPermission_addNewRole.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string roleId, int accountAccess, bool isVoter, bool isAdminRole, object id = null)
        {
            return base.SendRequestAsync(id, orgId, roleId, accountAccess, isVoter, isAdminRole);
        }
        public RpcRequest BuildRequest(string orgId, string roleId, int accountAccess, bool isVoter, bool isAdminRole, object id = null)
        {
            return base.BuildRequest(id, orgId, roleId, accountAccess, isVoter, isAdminRole);
        }
    }

}

