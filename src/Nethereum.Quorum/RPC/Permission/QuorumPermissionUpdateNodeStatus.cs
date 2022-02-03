
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Updates the status of the specified node. This method can be called by an organization admin account.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the node belongs
/// 
/// enodeId: string - complete enode ID
/// 
/// action: number -
/// 
/// 1 - for deactivating the node
/// 
/// 2 - for activating the deactivated node
/// 
/// 3 - for denylisting (blacklisting) the node
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionUpdateNodeStatus : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionUpdateNodeStatus(IClient client) : base(client,ApiMethods.quorumPermission_updateNodeStatus.ToString()) { }

        public Task<string> SendRequestAsync(string orgId, string enodeId, int action, object id = null)
        {
            return base.SendRequestAsync(id, orgId, enodeId, action);
        }
        public RpcRequest BuildRequest(string orgId, string enodeId, int action, object id = null)
        {
            return base.BuildRequest(id, orgId, enodeId, action);
        }
    }

}

