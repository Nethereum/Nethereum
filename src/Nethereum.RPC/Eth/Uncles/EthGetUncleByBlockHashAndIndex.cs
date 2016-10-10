using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Uncles
{
    /// <Summary>
    ///     eth_getUncleByBlockHashAndIndex
    ///     Returns information about a uncle of a block by hash and uncle index position.
    ///     Parameters
    ///     1. DATA, 32 Bytes - hash a block. 2.QUANTITY - the uncle's index position.
    ///     params: [
    ///     '0xc6ef2fc5426d6ad6fd9e2a26abeab0aa2411b7ab17f30a99d3cb96aed1d1055b',
    ///     '0x0' // 0
    ///     ]
    ///     Returns
    ///     Returns
    ///     See eth_getBlockByHash
    ///     Note: An uncle doesn't contain individual transactions.
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getUncleByBlockHashAndIndex","params":["0xc6ef2fc5426d6ad6fd9e2a26abeab0aa2411b7ab17f30a99d3cb96aed1d1055b",
    ///     "0x0"],"id":1}'
    /// </Summary>
    public class EthGetUncleByBlockHashAndIndex : RpcRequestResponseHandler<BlockWithTransactionHashes>
    {
        public EthGetUncleByBlockHashAndIndex(IClient client)
            : base(client, ApiMethods.eth_getUncleByBlockHashAndIndex.ToString())
        {
        }

        public Task<BlockWithTransactionHashes> SendRequestAsync(string blockHash, HexBigInteger uncleIndex,
            object id = null)
        {
            return base.SendRequestAsync(id, blockHash, uncleIndex);
        }

        public RpcRequest BuildRequest(string blockHash, HexBigInteger uncleIndex, object id = null)
        {
            return base.BuildRequest(id, blockHash, uncleIndex);
        }
    }
}