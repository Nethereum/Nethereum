using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Uncles
{
    /// <Summary>
    ///     eth_getUncleByBlockNumberAndIndex
    ///     Returns information about a uncle of a block by number and uncle index position.
    ///     Parameters
    ///     QUANTITY|TAG - a block number, or the string "earliest", "latest" or "pending", as in the default block parameter.
    ///     QUANTITY - the uncle's index position.
    ///     Returns
    ///     QUANTITY - integer of the number of uncles in this block.
    ///     Example
    ///     params: [
    ///     '0x29c', // 668
    ///     '0x0' // 0
    ///     ]
    ///     Returns
    ///     See eth_getBlockByHash
    ///     Note: An uncle doesn't contain individual transactions.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_getUncleByBlockNumberAndIndex","params":["0x29c",
    ///     "0x0"],"id":1}'
    /// </Summary>
    public class EthGetUncleByBlockNumberAndIndex : RpcRequestResponseHandler<BlockWithTransactionHashes>
    {
        public EthGetUncleByBlockNumberAndIndex(IClient client)
            : base(client, ApiMethods.eth_getUncleByBlockNumberAndIndex.ToString())
        {
        }

        public Task<BlockWithTransactionHashes> SendRequestAsync(BlockParameter blockParameter, HexBigInteger uncleIndex, object id = null)
        {
            return base.SendRequestAsync(id, blockParameter, uncleIndex);
        }

        public RpcRequest BuildRequest(BlockParameter blockParameter, HexBigInteger uncleIndex, object id = null)
        {
            return base.BuildRequest(id, blockParameter, uncleIndex);
        }
    }
}
