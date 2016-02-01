using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Generic;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth
{

    ///<Summary>
       /// eth_getTransactionByBlockNumberAndIndex
/// 
/// Returns information about a transaction by block number and transaction index position.
/// 
/// Parameters
/// 
/// QUANTITY|TAG - a block number, or the string "earliest", "latest" or "pending", as in the default block parameter.
/// QUANTITY - the transaction index position.
/// params: [
///    '0x29c', // 668
///    '0x0' // 0
/// ]
/// Returns
/// 
/// Transaction
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getTransactionByBlockNumberAndIndex","params":["0x29c", "0x0"],"id":1}'
/// Result see eth_getTransactionByHash
/// 
///     
    ///</Summary>
    public class EthGetTransactionByBlockNumberAndIndex : RpcRequestResponseHandler<Transaction>
        {
            public EthGetTransactionByBlockNumberAndIndex(RpcClient client) : base(client, ApiMethods.eth_getTransactionByBlockNumberAndIndex.ToString()) { }

            public async Task<Transaction> SendRequestAsync( HexBigInteger blockNumber, HexBigInteger transactionIndex, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync( id, blockNumber, transactionIndex);
            }
            public RpcRequest BuildRequest(HexBigInteger blockNumber, HexBigInteger transactionIndex, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, blockNumber, transactionIndex);
            }
        }

    }

