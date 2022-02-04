
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Initiates the recovery of the specified denylisted (blacklisted) account. This method can be called by a network admin account. Once a majority of the network admins approve, the denylisted account is marked as active.
    /// 
    /// Parameters
    /// orgId: string - organization or sub-organization ID to which the node belongs
    /// 
    /// acctId: string - denylisted account ID
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionRecoverBlackListedAccount
    {
        Task<string> SendRequestAsync(string orgId, string acctId, object id = null);
        RpcRequest BuildRequest(string orgId, string acctId, object id = null);
    }

    ///<Summary>
/// Initiates the recovery of the specified denylisted (blacklisted) account. This method can be called by a network admin account. Once a majority of the network admins approve, the denylisted account is marked as active.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the node belongs
/// 
/// acctId: string - denylisted account ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionRecoverBlackListedAccount : RpcRequestResponseHandler<string>, IQuorumPermissionRecoverBlackListedAccount
    {
        public QuorumPermissionRecoverBlackListedAccount(IClient client) : base(client,ApiMethods.quorumPermission_recoverBlackListedAccount.ToString()) { }

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

