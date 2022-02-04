
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Adds a new account as network admin or changes the organization admin account for an organization. This method can be called by a network admin account. Once a majority of the network admins approve, the role is approved.
    /// 
    /// Parameters
    /// orgId: string - organization ID to which the account belongs
    /// 
    /// acctId: string - account ID
    /// 
    /// roleId: string - new role ID to be assigned to the account; this can be the network admin role or an organization admin role only.
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionAssignAdminRole
    {
        Task<string> SendRequestAsync(string acctId, string roleId, object id = null);
        RpcRequest BuildRequest(string acctId, string roleId, object id = null);
    }

    ///<Summary>
/// Adds a new account as network admin or changes the organization admin account for an organization. This method can be called by a network admin account. Once a majority of the network admins approve, the role is approved.
/// 
/// Parameters
/// orgId: string - organization ID to which the account belongs
/// 
/// acctId: string - account ID
/// 
/// roleId: string - new role ID to be assigned to the account; this can be the network admin role or an organization admin role only.
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionAssignAdminRole : RpcRequestResponseHandler<string>, IQuorumPermissionAssignAdminRole
    {
        public QuorumPermissionAssignAdminRole(IClient client) : base(client,ApiMethods.quorumPermission_assignAdminRole.ToString()) { }

        public Task<string> SendRequestAsync(string acctId, string roleId, object id = null)
        {
            return base.SendRequestAsync(id, acctId, roleId);
        }
        public RpcRequest BuildRequest(string acctId, string roleId, object id = null)
        {
            return base.BuildRequest(id, acctId, roleId);
        }
    }

}

