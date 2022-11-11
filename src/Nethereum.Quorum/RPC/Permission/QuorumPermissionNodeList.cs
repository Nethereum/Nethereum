
using System.Threading.Tasks;
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
    public interface IQuorumPermissionNodeList
    {
        Task<JArray> SendRequestAsync(object id);
        RpcRequest BuildRequest(object id = null);
    }

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
    public class QuorumPermissionNodeList : GenericRpcRequestResponseHandlerNoParam<JArray>, IQuorumPermissionNodeList
    {
        public QuorumPermissionNodeList(IClient client) : base(client, ApiMethods.quorumPermission_nodeList.ToString()) { }
    }

}
