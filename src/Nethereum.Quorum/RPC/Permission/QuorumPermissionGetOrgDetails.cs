
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Returns lists of accounts, nodes, roles, and sub-organizations linked to the specified organization.
    /// 
    /// Parameters
    /// orgId: string - organization or sub-organization ID
    /// 
    /// Returns
    /// result: object - result object with the following fields:
    /// 
    /// acctList: array of objects - list of account objects
    /// 
    /// nodeList: array of objects - list of node objects
    /// 
    /// roleList: array of objects - list of role objects
    /// 
    /// subOrgList: array of objects - list of sub-organization objects    
    ///</Summary>
    public interface IQuorumPermissionGetOrgDetails
    {
        Task<JObject> SendRequestAsync(string orgId, object id = null);
        RpcRequest BuildRequest(string orgId, object id = null);
    }

    ///<Summary>
/// Returns lists of accounts, nodes, roles, and sub-organizations linked to the specified organization.
/// 
/// Parameters
/// orgId: string - organization or sub-organization ID
/// 
/// Returns
/// result: object - result object with the following fields:
/// 
/// acctList: array of objects - list of account objects
/// 
/// nodeList: array of objects - list of node objects
/// 
/// roleList: array of objects - list of role objects
/// 
/// subOrgList: array of objects - list of sub-organization objects    
///</Summary>
    public class QuorumPermissionGetOrgDetails : RpcRequestResponseHandler<JObject>, IQuorumPermissionGetOrgDetails
    {
        public QuorumPermissionGetOrgDetails(IClient client) : base(client,ApiMethods.quorumPermission_getOrgDetails.ToString()) { }

        public Task<JObject> SendRequestAsync(string orgId, object id = null)
        {
            return base.SendRequestAsync(id, orgId);
        }
        public RpcRequest BuildRequest(string orgId, object id = null)
        {
            return base.BuildRequest(id, orgId);
        }
    }

}

