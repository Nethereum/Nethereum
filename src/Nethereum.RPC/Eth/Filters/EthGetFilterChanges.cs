using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth.Filters
{

    ///<Summary>
    /// Polling method for a filter, which returns an array of logs which occurred since last poll.
    /// 
    /// Parameters
    /// 
    /// QUANTITY - the filter id.
    /// params: [
    ///   "0x16" // 22
    /// ]
    /// Returns
    /// 
    /// Array - Array of log objects, or an empty array if nothing has changed since last poll.
    /// 
    /// For filters created with eth_newBlockFilter the return are block hashes (DATA, 32 Bytes), e.g. ["0x3454645634534..."].
    /// For filters created with eth_newPendingTransactionFilter the return are transaction hashes (DATA, 32 Bytes), e.g. ["0x6345343454645..."].
    /// For filters created with eth_newFilter logs are objects with following params:
    /// 
    /// type: TAG - pending when the log is pending. mined if log is already mined.
    /// logIndex: QUANTITY - integer of the log index position in the block. null when its pending log.
    /// transactionIndex: QUANTITY - integer of the transactions index position log was created from. null when its pending log.
    /// transactionHash: DATA, 32 Bytes - hash of the transactions this log was created from. null when its pending log.
    /// blockHash: DATA, 32 Bytes - hash of the block where this log was in. null when its pending. null when its pending log.
    /// blockNumber: QUANTITY - the block number where this log was in. null when its pending. null when its pending log.
    /// address: DATA, 20 Bytes - address from which this log originated.
    /// data: DATA - contains one or more 32 Bytes non-indexed arguments of the log.
    /// topics: Array of DATA - Array of 0 to 4 32 Bytes DATA of indexed log arguments. (In solidity: The first topic is the hash of the signature of the event (e.g. Deposit(address,bytes32,uint256)), except you declared the event with the anonymous specifier.)
    /// Example
    /// 
    ///  Request
    /// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getFilterChanges","params":["0x16"],"id":73}'
    /// 
    ///  Result
    /// {
    ///   "id":1,
    ///   "jsonrpc":"2.0",
    ///   "result": [{
    ///     "logIndex": "0x1", // 1
    ///     "blockNumber":"0x1b4" // 436
    ///     "blockHash": "0x8216c5785ac562ff41e2dcfdf5785ac562ff41e2dcfdf829c5a142f1fccd7d",
    ///     "transactionHash":  "0xdf829c5a142f1fccd7d8216c5785ac562ff41e2dcfdf5785ac562ff41e2dcf",
    ///     "transactionIndex": "0x0", // 0
    ///     "address": "0x16c5785ac562ff41e2dcfdf829c5a142f1fccd7d",
    ///     "data":"0x0000000000000000000000000000000000000000000000000000000000000000",
    ///     "topics": ["0x59ebeb90bc63057b6515673c3ecf9438e5058bca0f92585014eced636878c9a5"]
    ///     },{
    ///       ...
    ///     }]
    /// }    
    ///</Summary>
    public class EthGetFilterChangesForEthNewFilter : RpcRequestResponseHandler<FilterLog[]>
    {
        public EthGetFilterChangesForEthNewFilter(RpcClient client)
            : base(client, ApiMethods.eth_getFilterChanges.ToString())
        {
        }

        public async Task<FilterLog[]> SendRequestAsync(HexBigInteger filterId,
            string id = Constants.DEFAULT_REQUEST_ID)
        {
            return await base.SendRequestAsync(id, filterId);
        }

        public RpcRequest BuildRequest(HexBigInteger filterId, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.BuildRequest(id, filterId);
        }
    }
}

    

