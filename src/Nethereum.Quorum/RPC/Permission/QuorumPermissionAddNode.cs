
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Adds a node to the specified organization or sub-organization. This method can be called by an organization admin account. A node cannot be part of multiple organizations.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID to which the node belongs
/// 
/// enodeId: string - complete enode ID
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionAddNode : RpcRequestResponseHandler<string>
    {
        public QuorumPermissionAddNode(IClient client) : base(client,ApiMethods.quorumPermission_addNode.ToString()) { }

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

