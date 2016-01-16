using System.Threading.Tasks;
using edjCase.JsonRpc.Client;

namespace Ethereum.RPC.Eth
{
    ///<Summary>
    /// eth_syncing
    /// 
    /// Returns an object object with data about the sync status or FALSE.
    /// 
    /// Parameters
    /// 
    /// none
    /// 
    /// Returns
    /// 
    /// Object|Boolean, An object with sync status data or FALSE, when not syncing:
    /// 
    /// startingBlock: QUANTITY - The block at which the import started (will only be reset, after the sync reached his head)
    /// currentBlock: QUANTITY - The current block, same as eth_blockNumber
    /// highestBlock: QUANTITY - The estimated highest block
    /// Example
    /// 
    ///  Request
    /// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_syncing","params":[],"id":1}'
    /// 
    ///  Result
    /// {
    ///   "id":1,
    ///   "jsonrpc": "2.0",
    ///   "result": {
    ///     startingBlock: '0x384',
    ///     currentBlock: '0x386',
    ///     highestBlock: '0x454'
    ///   }
    /// }
    ///  Or when not syncing
    /// {
    ///   "id":1,
    ///   "jsonrpc": "2.0",
    ///   "result": false
    /// }    
    ///</Summary>
    public class EthSyncing : GenericRpcRequestResponseHandlerNoParam<dynamic>
    {
        public EthSyncing() : base(ApiMethods.eth_syncing.ToString())
        {

        }

        public new async Task<EthSyncingOutput> SendRequestAsync(RpcClient client, string id = Constants.DEFAULT_REQUEST_ID)
        {
            var response = await base.SendRequestAsync(client, id);

            if (response is bool && response == false) return new EthSyncingOutput { Synching = response };

            return new EthSyncingOutput {
                                         Synching = true,
                                         CurrentBlock = new HexBigInteger(response.currentBlock),
                                         HighestBlock = new HexBigInteger(response.highestBlock),
                                         StartingBlock = new HexBigInteger(response.startingBlock)
                                        };
        }
    }
}
