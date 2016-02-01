using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Eth
{

    ///<Summary>
       /// eth_getUncleCountByBlockNumber
/// 
/// Returns the number of uncles in a block from a block matching the given block number.
/// 
/// Parameters
/// 
/// QUANTITY - integer of a block number, or the string "latest", "earliest" or "pending", see the default block parameter
/// params: [
///    '0xe8', // 232
/// ]
/// Returns
/// 
/// QUANTITY - integer of the number of uncles in this block.
/// 
/// Example
/// 
///  Request
/// curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getUncleCountByBlockNumber","params":["0xe8"],"id":1}'
/// 
///  Result
/// {
///   "id":1,
///   "jsonrpc": "2.0",
///   "result": "0x1" // 1
/// }    
    ///</Summary>
    public class EthGetUncleCountByBlockNumber : RpcRequestResponseHandler<HexBigInteger>
        {
            public EthGetUncleCountByBlockNumber(RpcClient client) : base(client, ApiMethods.eth_getUncleCountByBlockNumber.ToString()) { }

            public async Task<HexBigInteger> SendRequestAsync( HexBigInteger blockNumber, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return await base.SendRequestAsync( id, blockNumber);
            }
            public RpcRequest BuildRequest(HexBigInteger blockNumber, string id = Constants.DEFAULT_REQUEST_ID)
            {
                return base.BuildRequest(id, blockNumber);
            }
        }

    }

