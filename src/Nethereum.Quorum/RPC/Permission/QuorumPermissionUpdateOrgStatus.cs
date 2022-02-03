
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Temporarily suspends the specified organization or re-activates the specified suspended organization. This method can be called by a network admin account. This can only be performed for the master organization and requires the majority of network admins to approve.
/// 
/// Parameters
/// orgId: string - organization ID
/// 
/// action: number -
/// 
/// 1 - for suspending the organization
/// 
/// 2 - for activating the suspended organization
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionUpdateOrgStatus : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionUpdateOrgStatus(IClient client) : base(client,ApiMethods.quorumPermission_updateOrgStatus.ToString()) { }

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

