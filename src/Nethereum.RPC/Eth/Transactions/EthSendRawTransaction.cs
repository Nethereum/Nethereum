using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Transactions
{

    ///<Summary>
       /// eth_sendRawTransaction
/// 
/// Creates new message call transaction or a contract creation for signed transactions.
/// 
/// Parameters
/// 
/// DATA, The signed transaction data.
/// params: ["0xd46e8dd67c5d32be8d46e8dd67c5d32be8058bb8eb970870f072445675058bb8eb970870f072445675"]
/// Returns
/// 
/// DATA, 32 Bytes - the transaction hash, or the zero hash if the transaction is not yet available.
/// 
/// Use eth_getTransactionReceipt to get the contract address, after the transaction was mined, when you created a contract.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sendRawTransaction","params":[{see above}],"id":1}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc": "2.0",
///   "result": "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331"
/// }
///     
    ///</Summary>
    public class EthSendRawTransaction : RpcRequestResponseHandler<string>
        {
            public EthSendRawTransaction(RpcClient client) : base(client, ApiMethods.eth_sendRawTransaction.ToString()) { }

            public async Task<string> SendRequestAsync( string signedTransactionData, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync( id, signedTransactionData);
            }
            public RpcRequest BuildRequest(string signedTransactionData, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, signedTransactionData);
            }
        }

    }

