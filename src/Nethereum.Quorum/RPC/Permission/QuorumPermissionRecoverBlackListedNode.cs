
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Initiates the recovery of the specified denylisted (blacklisted) node. This method can be called by a network admin account. Once the majority of network admins approve, the denylisted node is marked as active.
    /// 
    /// Parameters
    /// orgId: string - organization or sub-organization ID to which the node belongs
    /// 
    /// enodeId: string - complete enode ID
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionRecoverBlackListedNode
    {
        Task<string> SendRequestAsync(string orgId, string enodeId, object id = null);
        RpcRequest BuildRequest(string orgId, string enodeId, object id = null);
    }

    ///<Summary>
/// Initiates the recovery of the specified denylisted (blacklisted) node. This method can be called by a network admin account. Once the majority of network admins approve, the denylisted node is marked as active.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the node belongs
/// 
/// enodeId: string - complete enode ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionRecoverBlackListedNode : RpcRequestResponseHandler<string>, IQuorumPermissionRecoverBlackListedNode
    {
        public QuorumPermissionRecoverBlackListedNode(IClient client) : base(client,ApiMethods.quorumPermission_recoverBlackListedNode.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string enodeId, object id = null)
        {
            return base.SendRequestAsync(id, orgId, enodeId);
        }
        public RpcRequest BuildRequest(string orgId, string enodeId, object id = null)
        {
            return base.BuildRequest(id, orgId, enodeId);
        }
    }

}

