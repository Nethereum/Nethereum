using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_getTransactionByHash
    ///     Returns the information about a transaction requested by transaction hash.
    ///     Parameters
    ///     DATA, 32 Bytes - hash of a transaction
    ///     params: [
    ///     "0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238"
    ///     ]
    ///     Returns
    ///     Object - A transaction object, or null when no transaction was found:
    ///     hash: DATA, 32 Bytes - hash of the transaction.
    ///     nonce: QUANTITY - the number of transactions made by the sender prior to this one.
    ///     blockHash: DATA, 32 Bytes - hash of the block where this transaction was in. null when its pending.
    ///     blockNumber: QUANTITY - block number where this transaction was in. null when its pending.
    ///     transactionIndex: QUANTITY - integer of the transactions index position in the block. null when its pending.
    ///     from: DATA, 20 Bytes - address of the sender.
    ///     to: DATA, 20 Bytes - address of the receiver. null when its a contract creation transaction.
    ///     value: QUANTITY - value transferred in Wei.
    ///     gasPrice: QUANTITY - gas price provided by the sender in Wei.
    ///     gas: QUANTITY - gas provided by the sender.
    ///     input: DATA - the data send along with the transaction.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getTransactionByHash","params":["0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc":"2.0",
    ///     "result": {
    ///     "hash":"0xc6ef2fc5426d6ad6fd9e2a26abeab0aa2411b7ab17f30a99d3cb96aed1d1055b",
    ///     "nonce":"0x",
    ///     "blockHash": "0xbeab0aa2411b7ab17f30a99d3cb9c6ef2fc5426d6ad6fd9e2a26a6aed1d1055b",
    ///     "blockNumber": "0x15df", // 5599
    ///     "transactionIndex":  "0x1", // 1
    ///     "from":"0x407d73d8a49eeb85d32cf465507dd71d507100c1",
    ///     "to":"0x85h43d8a49eeb85d32cf465507dd71d507100c1",
    ///     "value":"0x7f110" // 520464
    ///     "gas": "0x7f110" // 520464
    ///     "gasPrice":"0x09184e72a000",
    ///     "input":"0x603880600c6000396000f300603880600c6000396000f3603880600c6000396000f360",
    ///     }
    ///     }
    /// </Summary>
    public class EthGetTransactionByHash : RpcRequestResponseHandler<Transaction>, IEthGetTransactionByHash
    {
        public EthGetTransactionByHash(IClient client) : base(client, ApiMethods.eth_getTransactionByHash.ToString())
        {
        }

        public Task<Transaction> SendRequestAsync(string hashTransaction, object id = null)
        {
            return base.SendRequestAsync(id, hashTransaction.EnsureHexPrefix());
        }

        public RpcRequestResponseBatchItem<EthGetTransactionByHash, Transaction> CreateBatchItem(string transactionHash, object id)
        {
            return new RpcRequestResponseBatchItem<EthGetTransactionByHash, Transaction>(this, BuildRequest(transactionHash, id));
        }

       

#if !DOTNET35
        public async Task<List<Transaction>> SendBatchRequestAsync(string[] transactionHashes)
        {
            var batchRequest = new RpcRequestResponseBatch();
            for (int i = 0; i < transactionHashes.Length; i++)
            {
                batchRequest.BatchItems.Add(CreateBatchItem(transactionHashes[i], i));
            }

            var response = await Client.SendBatchRequestAsync(batchRequest);
            return response.BatchItems.Select(x => ((RpcRequestResponseBatchItem<EthGetTransactionByHash, Transaction>)x).Response).ToList();

        }
#endif

        public RpcRequest BuildRequest(string hashTransaction, object id = null)
        {
            return base.BuildRequest(id, hashTransaction.EnsureHexPrefix());
        }
    }
}