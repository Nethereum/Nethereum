using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Blocks
{
    /// <Summary>
    ///     eth_getBlockTransactionCountByHash
    ///     Returns the number of transactions in a block from a block matching the given block hash.
    ///     Parameters
    ///     DATA, 32 Bytes - hash of a block
    ///     params: [
    ///     '0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238'
    ///     ]
    ///     Returns
    ///     QUANTITY - integer of the number of transactions in this block.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getBlockTransactionCountByHash","params":["0xb903239f8543d04b5dc1ba6579132b143087c68db1b2168786408fcbce568238"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0xb" // 11
    ///     }
    /// </Summary>
    public class EthGetBlockTransactionCountByHash : RpcRequestResponseHandler<HexBigInteger>
    {
        public EthGetBlockTransactionCountByHash(IClient client)
            : base(client, ApiMethods.eth_getBlockTransactionCountByHash.ToString())
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