
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Permission
{
    ///<Summary>
    /// Creates a sub-organization under the master organization. This method can be called by an organization admin account.
    /// 
    /// Parameters
    /// parentOrgId: string - parent organization ID under which the sub-organization is being added
    /// 
    /// The parent organization ID should contain the complete organization hierarchy from master organization ID to the immediate parent. The organization hierarchy is separated by . (dot character). For example, if master organization ABC has a sub-organization SUB1, then while creating the sub-organization at SUB1 level, the parent organization should be given as ABC.SUB1.
    /// 
    /// 
    /// subOrgId: string - sub-organization ID
    /// 
    /// enodeId: string - complete enode ID of the node linked to the sub-organization ID; if left as an empty string, inherits the enode ID from the parent organization.
    /// 
    /// Returns
    /// result: string - response message    
    ///</Summary>
    public interface IQuorumPermissionAddSubOrg
    {
        Task<string> SendRequestAsync(string parentOrgId, string subOrgId, string enodeId, object id = null);
        RpcRequest BuildRequest(string parentOrgId, string subOrgId, string enodeId, object id = null);
    }

    ///<Summary>
/// Creates a sub-organization under the master organization. This method can be called by an organization admin account.
/// 
/// Parameters
/// parentOrgId: string - parent organization ID under which the sub-organization is being added
/// 
/// The parent organization ID should contain the complete organization hierarchy from master organization ID to the immediate parent. The organization hierarchy is separated by . (dot character). For example, if master organization ABC has a sub-organization SUB1, then while creating the sub-organization at SUB1 level, the parent organization should be given as ABC.SUB1.
/// 
/// 
/// subOrgId: string - sub-organization ID
/// 
/// enodeId: string - complete enode ID of the node linked to the sub-organization ID; if left as an empty string, inherits the enode ID from the parent organization.
/// 
/// Returns
/// result: string - response message    
///</Summary>
    public class QuorumPermissionAddSubOrg : RpcRequestResponseHandler<string>, IQuorumPermissionAddSubOrg
    {
        public QuorumPermissionAddSubOrg(IClient client) : base(client,ApiMethods.quorumPermission_addSubOrg.ToString()) { }

        public Task<string> SendRequestAsync(string parentOrgId, string subOrgId, string enodeId, object id = null)
        {
            return base.SendRequestAsync(id, parentOrgId, subOrgId, enodeId);
        }
        public RpcRequest BuildRequest(string parentOrgId, string subOrgId, string enodeId, object id = null)
        {
            return base.BuildRequest(id, parentOrgId, subOrgId, enodeId);
        }
    }

}

