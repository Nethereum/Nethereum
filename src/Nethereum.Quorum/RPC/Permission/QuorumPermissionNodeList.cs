
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Returns a list of permissioned nodes in the network.
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: array of objects - list of permissioned node objects with the following fields:
/// 
/// orgId: string - organization ID to which the node belongs
/// 
/// status: number - node status
/// 
/// url: string - complete enode ID    
///</Summary>
    public class QuorumPermissionNodeList : GenericRpcRequestResponseHandlerNoParam<JArray>
    {
        public QuorumPermissionNodeList(IClient client) : base(client, ApiMethods.quorumPermission_nodeList.ToString()) { }
    }

}
