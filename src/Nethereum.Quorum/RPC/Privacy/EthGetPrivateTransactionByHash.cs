
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Retrieve the details of a privacy marker transaction‘s internal private transaction using the PMT’s transaction hash.
    /// 
    /// Parameters
    /// string - privacy marker transaction’s hash in hex format
    /// Returns
    /// object - private transaction (nil if caller is not a participant)    
    ///</Summary>
    public interface IEthGetPrivateTransactionByHash
    {
        Task<Transaction> SendRequestAsync(string privacyMarkerTransactionHash, object id = null);
        RpcRequest BuildRequest(string privacyMarkerTransactionHash, object id = null);
    }

    ///<Summary>
/// Retrieve the details of a privacy marker transaction‘s internal private transaction using the PMT’s transaction hash.
/// 
/// Parameters
/// string - privacy marker transaction’s hash in hex format
/// Returns
/// object - private transaction (nil if caller is not a participant)    
///</Summary>
    public class EthGetPrivateTransactionByHash : RpcRequestResponseHandler<Transaction>, IEthGetPrivateTransactionByHash
    {
        public EthGetPrivateTransactionByHash(IClient client) : base(client,ApiMethods.eth_getPrivateTransactionByHash.ToString()) { }

        public Task<Transaction> SendRequestAsync(string privacyMarkerTransactionHash, object id = null)
        {
            return base.SendRequestAsync(id, privacyMarkerTransactionHash);
        }
        public RpcRequest BuildRequest(string privacyMarkerTransactionHash, object id = null)
        {
            return base.BuildRequest(id, privacyMarkerTransactionHash);
        }
    }

}

