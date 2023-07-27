using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_call
    ///     Executes a new message call immediately without creating a transaction on the block chain.
    ///     Parameters
    ///     Object - The transaction call object
    ///     from: DATA, 20 Bytes - (optional) The address the transaction is send from.
    ///     to: DATA, 20 Bytes - The address the transaction is directed to.
    ///     gas: QUANTITY - (optional) Integer of the gas provided for the transaction execution. eth_call consumes zero gas,
    ///     but this parameter may be needed by some executions.
    ///     gasPrice: QUANTITY - (optional) Integer of the gasPrice used for each paid gas
    ///     value: QUANTITY - (optional) Integer of the value send with this transaction
    ///     data: DATA - (optional) Hash of the method signature and encoded parameters. For details see Ethereum Contract ABI
    ///     QUANTITY|TAG - integer block number, or the string "latest", "earliest" or "pending", see the default block
    ///     parameter
    ///     Returns
    ///     DATA - the return value of executed contract.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_call","params":[{see above}],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x0"
    ///     }
    /// </Summary>
    public class EthCall : RpcRequestResponseHandler<string>, IDefaultBlock, IEthCall
    {
        public EthCall(IClient client) : base(client, ApiMethods.eth_call.ToString())
        {
            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock { get; set; }

        public Task<string> SendRequestAsync(CallInput callInput, BlockParameter block, object id = null)
        {
            if (block == null) return SendRequestAsync(callInput, id);
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            return base.SendRequestAsync(id, callInput, block);
        }

        public Task<string> SendRequestAsync(CallInput callInput, object id = null)
        {
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            return base.SendRequestAsync(id, callInput, DefaultBlock);
        }

#if !DOTNET35
        public Task<List<string>> SendBatchRequestAsync(params CallInput[] callInputs)
        {
           return SendBatchRequestAsync(callInputs, DefaultBlock);
        }

        public async Task<List<string>> SendBatchRequestAsync(CallInput[] callInputs, BlockParameter block)
        {
            var batchRequest = new RpcRequestResponseBatch();
            for (int i = 0; i < callInputs.Length; i++)
            {
                batchRequest.BatchItems.Add(new RpcRequestResponseBatchItem<EthCall, string>(this, BuildRequest(callInputs[i], block, i)));
            }

            var response = await Client.SendBatchRequestAsync(batchRequest).ConfigureAwait(false);
            return response.BatchItems.Select(x => ((RpcRequestResponseBatchItem<EthCall, string>)x).Response).ToList();

        }
#endif

        public RpcRequest BuildRequest(CallInput callInput, BlockParameter block, object id = null)
        {
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            if (block == null) throw new ArgumentNullException(nameof(block));
            return base.BuildRequest(id, callInput, block);
        }
    }
}