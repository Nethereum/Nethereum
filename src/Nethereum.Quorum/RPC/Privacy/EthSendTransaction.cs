
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Quorum.RPC.Privacy
{

    ///<Summary>
    /// Sends the specified transaction to the network.
    /// 
    /// If the transaction is a contract creation, use web3.eth.getTransactionReceipt() (eth_getTransactionReceipt) to get the contract address after the transaction is mined.
    /// 
    /// Parameters¶
    /// transaction: object - transaction object to send, with the following fields:
    /// 
    /// from: string - address for the sending account; defaults to web3.eth.defaultAccount
    /// 
    /// to: string - (optional) destination address of the message; defaults to undefined
    /// 
    /// value: number - (optional) value transferred for the transaction in Wei, also the endowment if it’s a contract-creation transaction
    /// 
    /// gas: number - (optional) amount of gas to use for the transaction (unused gas is refunded)
    /// 
    /// data: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
    /// 
    /// input: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
    /// 
    /// nonce: number - (optional) integer of a nonce; allows you to overwrite your own pending transactions that use the same nonce
    /// 
    /// privateFrom: string - (optional) when sending a private transaction, the sending party’s base64-encoded public key to use; if not present and passing privateFor, use the default key as configured in the TransactionManager.
    /// 
    /// privateFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
    /// 
    /// privacyFlag: number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
    /// 
    /// mandatoryFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
    /// 
    /// callback: function - (optional) callback function; if you pass a callback, the HTTP request is made asynchronous.    
    ///</Summary>
    public interface IEthSendTransaction
    {
        Task<string> SendRequestAsync(PrivateTransactionInput transaction, object id = null);
        RpcRequest BuildRequest(PrivateTransactionInput transaction, object id = null);
    }

    ///<Summary>
/// Sends the specified transaction to the network.
/// 
/// If the transaction is a contract creation, use web3.eth.getTransactionReceipt() (eth_getTransactionReceipt) to get the contract address after the transaction is mined.
/// 
/// Parameters¶
/// transaction: object - transaction object to send, with the following fields:
/// 
/// from: string - address for the sending account; defaults to web3.eth.defaultAccount
/// 
/// to: string - (optional) destination address of the message; defaults to undefined
/// 
/// value: number - (optional) value transferred for the transaction in Wei, also the endowment if it’s a contract-creation transaction
/// 
/// gas: number - (optional) amount of gas to use for the transaction (unused gas is refunded)
/// 
/// data: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
/// 
/// input: data - (optional) either a byte string containing the associated data of the message, or in the case of a contract-creation transaction, the initialization code
/// 
/// nonce: number - (optional) integer of a nonce; allows you to overwrite your own pending transactions that use the same nonce
/// 
/// privateFrom: string - (optional) when sending a private transaction, the sending party’s base64-encoded public key to use; if not present and passing privateFor, use the default key as configured in the TransactionManager.
/// 
/// privateFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
/// 
/// privacyFlag: number - (optional) 0 for SP (default if not provided), 1 for PP, 2 for MPP, and 3 for PSV transactions
/// 
/// mandatoryFor: array of strings - (optional) when sending a private transaction, an array of the recipients’ base64-encoded public keys
/// 
/// callback: function - (optional) callback function; if you pass a callback, the HTTP request is made asynchronous.    
///</Summary>
    public class EthSendTransaction : RpcRequestResponseHandler<string>, IEthSendTransaction
    {
        public EthSendTransaction(IClient client) : base(client,ApiMethods.eth_sendTransaction.ToString()) { }

        public Task<string> SendRequestAsync(PrivateTransactionInput transaction, object id = null)
        {
            return base.SendRequestAsync(id, transaction);
        }
        public RpcRequest BuildRequest(PrivateTransactionInput transaction, object id = null)
        {
            return base.BuildRequest(id, transaction);
        }
    }

}

