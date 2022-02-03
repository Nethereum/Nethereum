
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Returns a list of roles in the network.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: array of objects - list of role objects with the following fields:
/// 
/// access: number - account access
/// 
/// active: boolean - indicates if the role is active or not
/// 
/// isAdmin: boolean - indicates if the role is organization admin role
/// 
/// isVoter: boolean - indicates if the role is enabled for voting - applicable only for network admin role
/// 
/// orgId: string - organization ID to which the role is linked
/// 
/// roleId: string - unique role ID    
///</Summary>
    public class QuorumPermissionRoleList : GenericRpcRequestResponseHandlerNoParam<JArray>
    {
        public QuorumPermissionRoleList(IClient client) : base(client, ApiMethods.quorumPermission_roleList.ToString()) { }
    }

}
