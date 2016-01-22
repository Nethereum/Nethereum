

using System;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Ethereum.RPC.Eth;
using Ethereum.RPC.Generic;
using RPCRequestResponseHandlers;

namespace Ethereum.RPC
{

    ///<Summary>
    /// eth_call
    /// 
    /// Executes a new message call immediately without creating a transaction on the block chain.
    /// 
    /// Parameters
    /// 
    /// Object - The transaction call object
    /// from: DATA, 20 Bytes - (optional) The address the transaction is send from.
    /// to: DATA, 20 Bytes - The address the transaction is directed to.
    /// gas: QUANTITY - (optional) Integer of the gas provided for the transaction execution. eth_call consumes zero gas, but this parameter may be needed by some executions.
    /// gasPrice: QUANTITY - (optional) Integer of the gasPrice used for each paid gas
    /// value: QUANTITY - (optional) Integer of the value send with this transaction
    /// data: DATA - (optional) Hash of the method signature and encoded parameters. For details see Ethereum Contract ABI
    /// QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block parameter
    /// Returns
    /// 
    /// DATA - the return value of executed contract.
    /// 
    /// Example
    /// 
    ///  Request
    /// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_call","params":[{see above}],"id":1}'
    /// 
    ///  Result
    /// {
    ///   "id":1,
    ///   "jsonrpc": "2.0",
    ///   "result": "0x0"
    /// }    
    ///</Summary>
    public class EthCall : RpcRequestResponseHandler<string>
    {
        public EthCall() : base(ApiMethods.eth_call.ToString()) { }

        public async Task<string> SendRequestAsync(RpcClient client, EthCallTransactionInput ethCallInput, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(client, id, ethCallInput, block);
        }

        public async Task<string> SendRequestAsync(RpcClient client, EthCallTransactionInput ethCallInput, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(client, id, ethCallInput, BlockParameter.CreateLatest());
        }

        public RpcRequest BuildRequest(EthCallTransactionInput ethCallInput, BlockParameter block, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, ethCallInput, block);
        }
    }

}

