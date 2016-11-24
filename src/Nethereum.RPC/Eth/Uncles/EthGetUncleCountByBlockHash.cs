using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.Eth.Uncles
{
    /// <Summary>
    ///     eth_getUncleCountByBlockHash
    ///     Returns the number of uncles in a block from a block matching the given block hash.
    ///     Parameters
    ///     DATA, 32 Bytes - hash of a block
    ///     params: [
    ///     '0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238'
    ///     ]
    ///     Returns
    ///     QUANTITY - integer of the number of uncles in this block.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getUncleCountByBlockHash","params":["0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238"],"id"Block:1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x1" // 1
    ///     }
    /// </Summary>
    public class EthGetUncleCountByBlockHash : RpcRequestResponseHandler<HexBigInteger>
    {
        public EthGetUncleCountByBlockHash(IClient client)
            : base(client, ApiMethods.eth_getUncleCountByBlockHash.ToString())
        {
        }

        public Task<HexBigInteger> SendRequestAsync(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return base.SendRequestAsync(id, hash.EnsureHexPrefix());
        }

        public RpcRequest BuildRequest(string hash, object id = null)
        {
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            return base.BuildRequest(id, hash.EnsureHexPrefix());
        }
    }
}