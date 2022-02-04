
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Adds an account to an organization and assigns a role to the account. This method can be called by an organization admin account.
    /// 
    /// The account can only be linked to a single organization or sub-organization.
    /// 
    /// Parameters
    /// acctId: string - account ID
    /// 
    /// orgId: string - organization ID
    /// 
    /// roleId: string - role ID
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionAddAccountToOrg
    {
        Task<string> SendRequestAsync(string accountId, string orgId, string roleId, object id = null);
        RpcRequest BuildRequest(string accountId, string orgId, string roleId, object id = null);
    }

    ///<Summary>
/// Adds an account to an organization and assigns a role to the account. This method can be called by an organization admin account.
/// 
/// The account can only be linked to a single organization or sub-organization.
/// 
/// Parameters
/// acctId: string - account ID
/// 
/// orgId: string - organization ID
/// 
/// roleId: string - role ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionAddAccountToOrg : RpcRequestResponseHandler<string>, IQuorumPermissionAddAccountToOrg
    {
        public QuorumPermissionAddAccountToOrg(IClient client) : base(client,ApiMethods.quorumPermission_addAccountToOrg.ToString()) { }

        public Task<string> SendRequestAsync(string accountId, string orgId, string roleId, object id = null)
        {
            return base.SendRequestAsync(id, accountId, orgId, roleId);
        }
        public RpcRequest BuildRequest(string accountId, string orgId, string roleId, object id = null)
        {
            return base.BuildRequest(id, accountId, orgId, roleId);
        }
    }

}

