
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.ContractExtensions
{
    ///<Summary>
    /// Lists all active contract extensions involving this node (either as initiator or receiver).
    /// 
    /// Parameters
    /// None
    /// 
    /// Returns
    /// result: array of objects - list of contract extension objects with the following fields:
    /// 
    /// managementContractAddress: string - address of the extension management contract
    /// 
    /// contractExtended: string - address of the private contract getting extended
    /// 
    /// creationData: data - Tessera hash of creation data for extension management contract
    /// 
    /// initiator: string - contract extension initiator’s Ethereum address
    /// 
    /// recipient: string - new participant’s Ethereum address; the participant must later approve the extension using this address.
    /// 
    /// recipientPtmKey: string - new participant’s Tessera public key    
    ///</Summary>
    public interface IQuorumExtensionActiveExtensionContracts
    {
        Task<JArray> SendRequestAsync(object id);
        RpcRequest BuildRequest(object id = null);
    }

    ///<Summary>
/// Lists all active contract extensions involving this node (either as initiator or receiver).
/// 
/// Parameters
/// None
/// 
/// Returns
/// result: array of objects - list of contract extension objects with the following fields:
/// 
/// managementContractAddress: string - address of the extension management contract
/// 
/// contractExtended: string - address of the private contract getting extended
/// 
/// creationData: data - Tessera hash of creation data for extension management contract
/// 
/// initiator: string - contract extension initiator’s Ethereum address
/// 
/// recipient: string - new participant’s Ethereum address; the participant must later approve the extension using this address.
/// 
/// recipientPtmKey: string - new participant’s Tessera public key    
///</Summary>
    public class QuorumExtensionActiveExtensionContracts : GenericRpcRequestResponseHandlerNoParam<JArray>, IQuorumExtensionActiveExtensionContracts
    {
        public QuorumExtensionActiveExtensionContracts(IClient client) : base(client, ApiMethods.quorumExtension_activeExtensionContracts.ToString()) { }
    }

}
