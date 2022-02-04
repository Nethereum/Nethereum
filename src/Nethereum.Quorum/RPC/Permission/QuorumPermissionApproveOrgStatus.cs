
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Approves an organization status change proposal. This method can be called by a network admin account. Once a majority of the network admins approve the status update, the organization status is updated.
    /// 
    /// When an organization is in suspended status, no transactions or contract deployment activities are allowed from any nodes linked to the organization and sub-organizations under it. Similarly, no transactions are allowed from any accounts linked to the organization.
    /// 
    /// Parameters
    /// orgId: string - organization ID
    /// 
    /// action: number -
    /// 
    /// 1 - for approving organization suspension
    /// 
    /// 2 - for approving activation of the suspended organization
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionApproveOrgStatus
    {
        Task<string> SendRequestAsync(string orgId, int action, object id = null);
        RpcRequest BuildRequest(string orgId, int action, object id = null);
    }

    ///<Summary>
/// Approves an organization status change proposal. This method can be called by a network admin account. Once a majority of the network admins approve the status update, the organization status is updated.
/// 
/// When an organization is in suspended status, no transactions or contract deployment activities are allowed from any nodes linked to the organization and sub-organizations under it. Similarly, no transactions are allowed from any accounts linked to the organization.
/// 
/// Parameters
/// orgId: string - organization ID
/// 
/// action: number -
/// 
/// 1 - for approving organization suspension
/// 
/// 2 - for approving activation of the suspended organization
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionApproveOrgStatus : RpcRequestResponseHandler<string>, IQuorumPermissionApproveOrgStatus
    {
        public QuorumPermissionApproveOrgStatus(IClient client) : base(client,ApiMethods.quorumPermission_approveOrgStatus.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, int action, object id = null)
        {
            return base.SendRequestAsync(id, orgId, action);
        }
        public RpcRequest BuildRequest(string orgId, int action, object id = null)
        {
            return base.BuildRequest(id, orgId, action);
        }
    }

}

