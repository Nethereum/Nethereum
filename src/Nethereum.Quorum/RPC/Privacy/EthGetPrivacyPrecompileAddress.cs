
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Get the address of the privacy precompile contract, to be used as the to address for privacy marker transactions.
    /// 
    /// Parameters
    /// None
    /// 
    /// Returns
    /// string - contract address for the privacy precompile in hex format    
    ///</Summary>
    public interface IEthGetPrivacyPrecompileAddress
    {
        Task<String> SendRequestAsync(object id);
        RpcRequest BuildRequest(object id = null);
    }

    ///<Summary>
/// Get the address of the privacy precompile contract, to be used as the to address for privacy marker transactions.
/// 
/// Parameters
/// None
/// 
/// Returns
/// string - contract address for the privacy precompile in hex format    
///</Summary>
    public class EthGetPrivacyPrecompileAddress : GenericRpcRequestResponseHandlerNoParam<string>, IEthGetPrivacyPrecompileAddress
    {
        public EthGetPrivacyPrecompileAddress(IClient client) : base(client, ApiMethods.eth_getPrivacyPrecompileAddress.ToString()) { }
    }

}
