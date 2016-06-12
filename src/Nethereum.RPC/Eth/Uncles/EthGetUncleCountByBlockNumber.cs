using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Uncles
{
    /// <Summary>
    ///     eth_getUncleCountByBlockNumber
    ///     Returns the number of uncles in a block from a block matching the given block number.
    ///     Parameters
    ///     QUANTITY - integer of a block number, or the string "latest", "earliest" or "pending", see the default block
    ///     parameter
    ///     params: [
    ///     '0xe8', // 232
    ///     ]
    ///     Returns
    ///     QUANTITY - integer of the number of uncles in this block.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getUncleCountByBlockNumber","params":["0xe8"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x1" // 1
    ///     }
    /// </Summary>
    public class EthGetUncleCountByBlockNumber : RpcRequestResponseHandler<HexBigInteger>
    {
        public EthGetUncleCountByBlockNumber(IClient client)
            : base(client, ApiMethods.eth_getUncleCountByBlockNumber.ToString())
        {
        }

        public Task<HexBigInteger> SendRequestAsync(HexBigInteger blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber);
        }
    }
}