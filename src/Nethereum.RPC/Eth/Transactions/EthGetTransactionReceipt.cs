using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     Returns the receipt of a transaction by transaction hash.
    ///     Note That the receipt is not available for pending transactions.
    ///     Parameters
    ///     DATA, 32 Bytes - hash of a transaction
    ///     params: [
    ///     '0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238'
    ///     ]
    ///     Returns
    ///     Object - A transaction receipt object, or null when no receipt was found:
    ///     transactionHash: DATA, 32 Bytes - hash of the transaction.
    ///     transactionIndex: QUANTITY - integer of the transactions index position in the block.
    ///     blockHash: DATA, 32 Bytes - hash of the block where this transaction was in.
    ///     blockNumber: QUANTITY - block number where this transaction was in.
    ///     cumulativeGasUsed: QUANTITY - The total amount of gas used when this transaction was executed in the block.
    ///     gasUsed: QUANTITY - The amount of gas used by this specific transaction alone.
    ///     contractAddress: DATA, 20 Bytes - The contract address created, if the transaction was a contract creation,
    ///     otherwise null.
    ///     logs: Array - Array of log objects, which this transaction generated.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getTransactionReceipt","params":["0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc":"2.0",
    ///     "result": {
    ///     transactionHash: '0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238',
    ///     transactionIndex:  '0x1', // 1
    ///     blockNumber: '0xb', // 11
    ///     blockHash: '0xc6ef2fc5426d6ad6fd9e2a26abeab0aa2411b7ab17f30a99d3cb96aed1d1055b',
    ///     cumulativeGasUsed: '0x33bc', // 13244
    ///     gasUsed: '0x4dc', // 1244
    ///     contractAddress: '0xb60e8dd61c5d32be8058bb8eb970870f07233155' // or null, if none was created
    ///     logs: [{
    ///     // logs as returned by getFilterLogs, etc.
    ///     }, ...]
    ///     }
    ///     }
    /// </Summary>
    public class EthGetTransactionReceipt : RpcRequestResponseHandler<TransactionReceipt>
    {
        public EthGetTransactionReceipt(IClient client) : base(client, ApiMethods.eth_getTransactionReceipt.ToString())
        {
        }

        public Task<TransactionReceipt> SendRequestAsync(string transactionHash, object id = null)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));
            return base.SendRequestAsync(id, transactionHash.EnsureHexPrefix());
        }

        public RpcRequest BuildRequest(string transactionHash, object id = null)
        {
            if (transactionHash == null) throw new ArgumentNullException(nameof(transactionHash));
            return base.BuildRequest(id, transactionHash.EnsureHexPrefix());
        }
    }
}