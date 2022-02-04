
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Approves the organization admin or network admin role assignment to the specified account. This method can be called by a network admin account. The role is approved once the majority of network admins approve.
    /// 
    /// Parameters
    /// orgId: string - organization ID to which the account belongs
    /// 
    /// acctId: string - account ID
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionApproveAdminRole
    {
        Task<string> SendRequestAsync(string orgId, string acctId, object id = null);
        RpcRequest BuildRequest(string orgId, string acctId, object id = null);
    }

    ///<Summary>
/// Approves the organization admin or network admin role assignment to the specified account. This method can be called by a network admin account. The role is approved once the majority of network admins approve.
/// 
/// Parameters
/// orgId: string - organization ID to which the account belongs
/// 
/// acctId: string - account ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionApproveAdminRole : RpcRequestResponseHandler<string>, IQuorumPermissionApproveAdminRole
    {
        public QuorumPermissionApproveAdminRole(IClient client) : base(client,ApiMethods.quorumPermission_approveAdminRole.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string acctId, object id = null)
        {
            return base.SendRequestAsync(id, orgId, acctId);
        }
        public RpcRequest BuildRequest(string orgId, string acctId, object id = null)
        {
            return base.BuildRequest(id, orgId, acctId);
        }
    }

}

