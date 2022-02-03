
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Permission
{

///<Summary>
/// Returns a list of all organizations with the status of each organization in the network.
/// 
/// Parameters¶
/// None
/// 
/// Returns¶
/// result: array of objects - list of organization objects with the following fields:
/// 
/// fullOrgId: string - complete organization ID including all the parent organization IDs separated by .
/// 
/// level: number - level of the organization in the organization hierarchy
/// 
/// orgId: string - organization ID
/// 
/// parentOrgId: string - immediate parent organization ID
/// 
/// status: number - organization status
/// 
/// subOrgList: array of strings - list of sub-organizations linked to the organization
/// 
/// ultimateParent: string - master organization under which the organization falls    
///</Summary>
    public class QuorumPermissionOrgList : GenericRpcRequestResponseHandlerNoParam<PermissionOrganisation[]>
    {
        public QuorumPermissionOrgList(IClient client) : base(client, ApiMethods.quorumPermission_orgList.ToString()) { }
    }

}
