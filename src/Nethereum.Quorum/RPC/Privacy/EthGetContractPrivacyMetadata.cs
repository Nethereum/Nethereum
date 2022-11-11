
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Queries the privacy metadata for the specified contract account address.
    /// 
    /// Parameter
    /// string - contract address
    /// 
    /// Returns
    /// result: object - result object with the following fields:
    /// 
    /// creationTxHash: data - affected contract’s original transaction’s encrypted payload hash
    /// 
    /// privacyFlag: number - 0 for SP, 1 for PP, 2 for MPP, and 3 for PSV transactions
    /// 
    /// mandatoryFor: string - an array of the recipients’ base64-encoded public keys    
    ///</Summary>
    public interface IEthGetContractPrivacyMetadata
    {
        Task<ContractPrivacyMetadata> SendRequestAsync(string address, object id = null);
        RpcRequest BuildRequest(string address, object id = null);
    }

    ///<Summary>
/// Queries the privacy metadata for the specified contract account address.
/// 
/// Parameter
/// string - contract address
/// 
/// Returns
/// result: object - result object with the following fields:
/// 
/// creationTxHash: data - affected contract’s original transaction’s encrypted payload hash
/// 
/// privacyFlag: number - 0 for SP, 1 for PP, 2 for MPP, and 3 for PSV transactions
/// 
/// mandatoryFor: string - an array of the recipients’ base64-encoded public keys    
///</Summary>
    public class EthGetContractPrivacyMetadata : RpcRequestResponseHandler<ContractPrivacyMetadata>, IEthGetContractPrivacyMetadata
    {
        public EthGetContractPrivacyMetadata(IClient client) : base(client,ApiMethods.eth_getContractPrivacyMetadata.ToString()) { }

        public Task<ContractPrivacyMetadata> SendRequestAsync(string address, object id = null)
        {
            return base.SendRequestAsync(id, address);
        }
        public RpcRequest BuildRequest(string address, object id = null)
        {
            return base.BuildRequest(id, address);
        }
    }

}

