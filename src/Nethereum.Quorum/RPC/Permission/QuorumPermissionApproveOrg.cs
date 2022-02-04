
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Approves the specified proposed organization into the network. This method can be called by a network admin account.
    /// 
    /// Parameters
    /// orgId: string - unique organization ID
    /// 
    /// enodeId: string - complete enode ID
    /// 
    /// accountId: string - account to be the organization admin account
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionApproveOrg
    {
        Task<string> SendRequestAsync(string orgId, string enodeId, string accountId, object id = null);
        RpcRequest BuildRequest(string orgId, string enodeId, string accountId, object id = null);
    }

    ///<Summary>
/// Approves the specified proposed organization into the network. This method can be called by a network admin account.
/// 
/// Parameters
/// orgId: string - unique organization ID
/// 
/// enodeId: string - complete enode ID
/// 
/// accountId: string - account to be the organization admin account
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionApproveOrg : RpcRequestResponseHandler<string>, IQuorumPermissionApproveOrg
    {
        public QuorumPermissionApproveOrg(IClient client) : base(client,ApiMethods.quorumPermission_approveOrg.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string enodeId, string accountId, object id = null)
        {
            return base.SendRequestAsync(id, orgId, enodeId, accountId);
        }
        public RpcRequest BuildRequest(string orgId, string enodeId, string accountId, object id = null)
        {
            return base.BuildRequest(id, orgId, enodeId, accountId);
        }
    }

}

