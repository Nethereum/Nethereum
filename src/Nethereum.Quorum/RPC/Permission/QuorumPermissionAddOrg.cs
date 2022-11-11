
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Proposes a new organization into the network. This method can be called by a network admin account.
    /// 
    /// If there are any pending items for approval, proposal of any new organization fails. Also, the enode ID and account ID can only be linked to one organization.
    /// 
    /// Parameter
    /// orgId: string - unique organization ID
    /// 
    /// enodeId: string - complete enode ID
    /// 
    /// accountId: string - account to be the organization admin account
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionAddOrg
    {
        Task<string> SendRequestAsync(string orgId, string enodeId, string accountId, object id = null);
        RpcRequest BuildRequest(string orgId, string enodeId, string accountId, object id = null);
    }

    ///<Summary>
/// Proposes a new organization into the network. This method can be called by a network admin account.
/// 
/// If there are any pending items for approval, proposal of any new organization fails. Also, the enode ID and account ID can only be linked to one organization.
/// 
/// Parameter
/// orgId: string - unique organization ID
/// 
/// enodeId: string - complete enode ID
/// 
/// accountId: string - account to be the organization admin account
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionAddOrg : RpcRequestResponseHandler<string>, IQuorumPermissionAddOrg
    {
        public QuorumPermissionAddOrg(IClient client) : base(client,ApiMethods.quorumPermission_addOrg.ToString()) { }

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

