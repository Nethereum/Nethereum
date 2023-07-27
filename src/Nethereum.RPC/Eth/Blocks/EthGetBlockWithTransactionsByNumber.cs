using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    /// <Summary>
    ///     eth_getBlockByNumber
    ///     Returns information about a block by block number.
    ///     Parameters
    ///     QUANTITY|TAG - integer of a block number, or the string "earliest", "latest" or "pending", as in the default block
    ///     parameter.
    ///     Boolean - If true it returns the full transaction objects, if false only the hashes of the transactions.
    ///     params: [
    ///     '0x1b4', // 436
    ///     true
    ///     ]
    ///     Returns
    ///     See eth_getBlockByHash
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getBlockByNumber","params":["0x1b4", true],"id":1}'
    ///     Returns
    ///     Object - A block object, or null when no block was found:
    ///     number: QUANTITY - the block number. null when its pending block.
    ///     hash: DATA, 32 Bytes - hash of the block. null when its pending block.
    ///     parentHash: DATA, 32 Bytes - hash of the parent block.
    ///     nonce: DATA, 8 Bytes - hash of the generated proof-of-work. null when its pending block.
    ///     sha3Uncles: DATA, 32 Bytes - SHA3 of the uncles data in the block.
    ///     logsBloom: DATA, 256 Bytes - the bloom filter for the logs of the block. null when its pending block.
    ///     transactionsRoot: DATA, 32 Bytes - the root of the transaction trie of the block.
    ///     stateRoot: DATA, 32 Bytes - the root of the final state trie of the block.
    ///     receiptsRoot: DATA, 32 Bytes - the root of the receipts trie of the block.
    ///     miner: DATA, 20 Bytes - the address of the beneficiary to whom the mining rewards were given.
    ///     difficulty: QUANTITY - integer of the difficulty for this block.
    ///     totalDifficulty: QUANTITY - integer of the total difficulty of the chain until this block.
    ///     extraData: DATA - the "extra data" field of this block.
    ///     size: QUANTITY - integer the size of this block in bytes.
    ///     gasLimit: QUANTITY - the maximum gas allowed in this block.
    ///     gasUsed: QUANTITY - the total used gas by all transactions in this block.
    ///     timestamp: QUANTITY - the unix timestamp for when the block was collated.
    ///     transactions: Array - Array of transaction objects, or 32 Bytes transaction hashes depending on the last given
    ///     parameter.
    ///     uncles: Array - Array of uncle hashes.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getBlockByHash","params":["0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331",
    ///     true],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc":"2.0",
    ///     "result": {
    ///     "number": "0x1b4", // 436
    ///     "hash": "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331",
    ///     "parentHash": "0x9646252be9520f6e71339a8df9c55e4d7619deeb018d2a3f2d21fc165dde5eb5",
    ///     "nonce": "0xe04d296d2460cfb8472af2c5fd05b5a214109c25688d3704aed5484f9a7792f2",
    ///     "sha3Uncles": "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
    ///     "logsBloom": "0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331",
    ///     "transactionsRoot": "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421",
    ///     "stateRoot": "0xd5855eb08b3387c0af375e9cdb6acfc05eb8f519e419b874b6ff2ffda7ed1dff",
    ///     "miner": "0x4e65fda2159562a496f9f3522f89122a3088497a",
    ///     "difficulty": "0x027f07", // 163591
    ///     "totalDifficulty":  "0x027f07", // 163591
    ///     "extraData": "0x0000000000000000000000000000000000000000000000000000000000000000",
    ///     "size":  "0x027f07", // 163591
    ///     "gasLimit": "0x9f759", // 653145
    ///     "minGasPrice": "0x9f759", // 653145
    ///     "gasUsed": "0x9f759", // 653145
    ///     "timestamp": "0x54e34e8e" // 1424182926
    ///     "transactions": [{...},{ ... }]
    ///     "uncles": ["0x1606e5...", "0xd5145a9..."]
    ///     }
    ///     }
    /// </Summary>
    public class EthGetBlockWithTransactionsByNumber : RpcRequestResponseHandler<BlockWithTransactions>, IEthGetBlockWithTransactionsByNumber
    {
        public EthGetBlockWithTransactionsByNumber(IClient client)
            : base(client, ApiMethods.eth_getBlockByNumber.ToString())
        {
        }

        public Task<BlockWithTransactions> SendRequestAsync(BlockParameter blockParameter, object id = null)
        {
            if (blockParameter == null) throw new ArgumentNullException(nameof(blockParameter));
            return base.SendRequestAsync(id, blockParameter, true);
        }

        public Task<BlockWithTransactions> SendRequestAsync(HexBigInteger number, object id = null)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            return base.SendRequestAsync(id, number, true);
        }

#if !DOTNET35
        public async Task<List<BlockWithTransactions>> SendBatchRequestAsync(params HexBigInteger[] numbers)
        {
            var batchRequest = new RpcRequestResponseBatch();
            for (int i = 0; i < numbers.Length; i++)
            {
                batchRequest.BatchItems.Add(new RpcRequestResponseBatchItem<EthGetBlockWithTransactionsByNumber, BlockWithTransactions>(this, BuildRequest(numbers[i], i)));
            }

            var response = await Client.SendBatchRequestAsync(batchRequest);
            return response.BatchItems.Select(x => ((RpcRequestResponseBatchItem<EthGetBlockWithTransactionsByNumber, BlockWithTransactions>)x).Response).ToList();

        }
#endif

        public RpcRequest BuildRequest(HexBigInteger number, object id = null)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            return base.BuildRequest(id, number, true);
        }

        public RpcRequest BuildRequest(BlockParameter blockParameter, object id = null)
        {
            if (blockParameter == null) throw new ArgumentNullException(nameof(blockParameter));
            return base.BuildRequest(id, blockParameter, true);
        }
    }
}