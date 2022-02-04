
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{
    ///<Summary>
    /// Supports offline signing of the specified transaction. This can be used to fill and sign both public and private transactions. Defaults to RLP plus json.
    /// 
    /// Parameters
    /// transaction: object - transaction object to send, with the following fields:
    /// 
    /// from: string - address for the sending account
    /// 
    /// to: string - (optional) destination address of the message
    /// 
    /// value: number - (optional) value transferred for the transaction in Wei, also the endowment if it’s a contract-creation transaction
    /// 
    /// data: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
    /// 
    /// privateFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
    /// 
    /// Returns
    /// result: object - result object with the following fields:
    /// 
    /// raw: data - RLP-encoded bytes for the passed transaction object
    /// 
    /// tx: object - transaction object    
    ///</Summary>
    public interface IEthFillTransaction
    {
        Task<FillTransactionResponse> SendRequestAsync(PrivateTransactionInput transaction, object id = null);
        RpcRequest BuildRequest(PrivateTransactionInput transaction, object id = null);
    }

    ///<Summary>
/// Supports offline signing of the specified transaction. This can be used to fill and sign both public and private transactions. Defaults to RLP plus json.
/// 
/// Parameters
/// transaction: object - transaction object to send, with the following fields:
/// 
/// from: string - address for the sending account
/// 
/// to: string - (optional) destination address of the message
/// 
/// value: number - (optional) value transferred for the transaction in Wei, also the endowment if it’s a contract-creation transaction
/// 
/// data: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
/// 
/// privateFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
/// 
/// Returns
/// result: object - result object with the following fields:
/// 
/// raw: data - RLP-encoded bytes for the passed transaction object
/// 
/// tx: object - transaction object    
///</Summary>
    public class EthFillTransaction : RpcRequestResponseHandler<FillTransactionResponse>, IEthFillTransaction
    {
        public EthFillTransaction(IClient client) : base(client,ApiMethods.eth_fillTransaction.ToString()) { }

        public Task<FillTransactionResponse> SendRequestAsync(PrivateTransactionInput transaction, object id = null)
        {
            return base.SendRequestAsync(id, transaction);
        }
        public RpcRequest BuildRequest(PrivateTransactionInput transaction, object id = null)
        {
            return base.BuildRequest(id, transaction);
        }
    }

}

