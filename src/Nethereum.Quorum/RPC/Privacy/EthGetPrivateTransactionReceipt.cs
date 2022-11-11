
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Retrieve the receipt of a privacy marker transaction’s (PMT) internal private transaction using the PMT’s transaction hash.
    /// 
    /// Parameters
    /// string - privacy marker transaction’s hash in hex format
    /// Returns
    /// object - private transaction receipt (nil if caller is not a participant)    
    ///</Summary>
    public interface IEthGetPrivateTransactionReceipt
    {
        Task<TransactionReceipt> SendRequestAsync(string privacyMarkerTransactionHash, object id = null);
        RpcRequest BuildRequest(string privacyMarkerTransactionHash, object id = null);
    }

    ///<Summary>
/// Retrieve the receipt of a privacy marker transaction’s (PMT) internal private transaction using the PMT’s transaction hash.
/// 
/// Parameters
/// string - privacy marker transaction’s hash in hex format
/// Returns
/// object - private transaction receipt (nil if caller is not a participant)    
///</Summary>
    public class EthGetPrivateTransactionReceipt : RpcRequestResponseHandler<TransactionReceipt>, IEthGetPrivateTransactionReceipt
    {
        public EthGetPrivateTransactionReceipt(IClient client) : base(client,ApiMethods.eth_getPrivateTransactionReceipt.ToString()) { }

        public Task<TransactionReceipt> SendRequestAsync(string privacyMarkerTransactionHash, object id = null)
        {
            return base.SendRequestAsync(id, privacyMarkerTransactionHash);
        }
        public RpcRequest BuildRequest(string privacyMarkerTransactionHash, object id = null)
        {
            return base.BuildRequest(id, privacyMarkerTransactionHash);
        }
    }

}

