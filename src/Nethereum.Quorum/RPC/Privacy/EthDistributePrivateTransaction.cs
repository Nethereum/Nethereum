
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{

    ///<Summary>
    /// Send a signed private transaction to the local private transaction manager and share with private participant’s transaction managers.
    /// 
    /// This API method is to be used as part of the process for sending externally signed privacy marker transactions. The private transaction should be signed, sent to participants with this API, and the resulting hash set as the PMT’s data.
    /// 
    /// Note
    /// 
    /// Two step process:
    /// 
    /// Performs the same as eth_sendRawPrivateTransaction (simulation and calling /sendsignedtx), but doesn’t submit private transaction to txpool.
    /// Sends the private transaction to Tessera to generate a hash, which should be placed in the privacy marker transaction.
    /// Parameters
    /// string - signed private transaction in hex format
    /// object - private data to send, with the following fields:
    /// privateFor: List<String> - an array of the recipients’ base64-encoded public keys
    /// privateFrom: String - (optional) the sending party’s base64-encoded public key to use (Privacy Manager default if not provided)
    /// privacyFlag: Number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
    /// mandatoryFor: List<String> - an array of the recipients’ base64-encoded public keys
    /// Returns
    /// string - Transaction Manager hash to be used as a privacy marker transaction’s data when externally signing    
    ///</Summary>
    public interface IEthDistributePrivateTransaction
    {
        Task<string> SendRequestAsync(string signedTxn, PrivateData privateData, object id = null);
        RpcRequest BuildRequest(string signedTxn, PrivateData privateData, object id = null);
    }

    ///<Summary>
/// Send a signed private transaction to the local private transaction manager and share with private participant’s transaction managers.
/// 
/// This API method is to be used as part of the process for sending externally signed privacy marker transactions. The private transaction should be signed, sent to participants with this API, and the resulting hash set as the PMT’s data.
/// 
/// Note
/// 
/// Two step process:
/// 
/// Performs the same as eth_sendRawPrivateTransaction (simulation and calling /sendsignedtx), but doesn’t submit private transaction to txpool.
/// Sends the private transaction to Tessera to generate a hash, which should be placed in the privacy marker transaction.
/// Parameters
/// string - signed private transaction in hex format
/// object - private data to send, with the following fields:
/// privateFor: List<String> - an array of the recipients’ base64-encoded public keys
/// privateFrom: String - (optional) the sending party’s base64-encoded public key to use (Privacy Manager default if not provided)
/// privacyFlag: Number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
/// mandatoryFor: List<String> - an array of the recipients’ base64-encoded public keys
/// Returns
/// string - Transaction Manager hash to be used as a privacy marker transaction’s data when externally signing    
///</Summary>
    public class EthDistributePrivateTransaction : RpcRequestResponseHandler<string>, IEthDistributePrivateTransaction
    {
        public EthDistributePrivateTransaction(IClient client) : base(client,ApiMethods.eth_distributePrivateTransaction.ToString()) { }

        public Task<string> SendRequestAsync(string signedTxn, PrivateData privateData, object id = null)
        {
            return base.SendRequestAsync(id, signedTxn, privateData);
        }
        public RpcRequest BuildRequest(string signedTxn, PrivateData privateData, object id = null)
        {
            return base.BuildRequest(id, signedTxn, privateData);
        }
    }

}

