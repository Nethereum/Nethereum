
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Returns a list of permissioned accounts in the network.
    /// 
    /// Parameters
    /// None
    /// 
    /// Returns
    /// result: array of objects - list of permissioned account objects with the following fields:
    /// 
    /// acctId: string - account ID
    /// 
    /// isOrgAdmin: boolean - indicates if the account is admin account for the organization
    /// 
    /// orgId: string - organization ID
    /// 
    /// roleId: string - role assigned to the account
    /// 
    /// status: number - account status    
    ///</Summary>
    public interface IQuorumPermissionAcctList
    {
        Task<PermissionAccount> SendRequestAsync(object id);
        RpcRequest BuildRequest(object id = null);
    }

    ///<Summary>
/// Returns a list of permissioned accounts in the network.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: array of objects - list of permissioned account objects with the following fields:
/// 
/// acctId: string - account ID
/// 
/// isOrgAdmin: boolean - indicates if the account is admin account for the organization
/// 
/// orgId: string - organization ID
/// 
/// roleId: string - role assigned to the account
/// 
/// status: number - account status    
///</Summary>
    public class QuorumPermissionAcctList : GenericRpcRequestResponseHandlerNoParam<PermissionAccount>, IQuorumPermissionAcctList
    {
        public QuorumPermissionAcctList(IClient client) : base(client, ApiMethods.quorumPermission_acctList.ToString()) { }
    }

}
