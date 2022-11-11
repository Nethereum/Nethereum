
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{

    ///<Summary>
    /// Parameters
    /// string - signed transaction data in hex format
    /// 
    /// object - private data to send, with the following fields:
    /// 
    /// privateFor: array of strings - when sending a private transaction, an array of the recipients’ base64-encoded public keys
    /// 
    /// privacyFlag: number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
    /// 
    /// mandatoryFor: array of strings - when sending a private transaction, an array of the recipients’ base64-encoded public keys
    /// 
    /// Returns
    /// result: string - 32-byte transaction hash as a hex string    
    ///</Summary>
    public interface IEthSendRawPrivateTransaction
    {
        Task<string> SendRequestAsync(string signedTransaction, PrivateData privateData, object id = null);
        RpcRequest BuildRequest(string signedTransaction, PrivateData privateData, object id = null);
    }

    ///<Summary>
/// Parameters
/// string - signed transaction data in hex format
/// 
/// object - private data to send, with the following fields:
/// 
/// privateFor: array of strings - when sending a private transaction, an array of the recipients’ base64-encoded public keys
/// 
/// privacyFlag: number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
/// 
/// mandatoryFor: array of strings - when sending a private transaction, an array of the recipients’ base64-encoded public keys
/// 
/// Returns
/// result: string - 32-byte transaction hash as a hex string    
///</Summary>
    public class EthSendRawPrivateTransaction : RpcRequestResponseHandler<string>, IEthSendRawPrivateTransaction
    {
        public EthSendRawPrivateTransaction(IClient client) : base(client,ApiMethods.eth_sendRawPrivateTransaction.ToString()) { }

        public Task<string> SendRequestAsync(string signedTransaction, PrivateData privateData, object id = null)
        {
            return base.SendRequestAsync(id, signedTransaction, privateData);
        }
        public RpcRequest BuildRequest(string signedTransaction, PrivateData privateData, object id = null)
        {
            return base.BuildRequest(id, signedTransaction, privateData);
        }
    }

}

