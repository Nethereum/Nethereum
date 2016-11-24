using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    /// <Summary>
    ///     eth_getTransactionByBlockHashAndIndex
    ///     Returns information about a transaction by block hash and transaction index position.
    ///     Parameters
    ///     DATA, 32 Bytes - hash of a block.
    ///     QUANTITY - integer of the transaction index position.
    ///     params: [
    ///     '0xe670ec64341771606e55d6b4ca35a1a6b75ee3d5145a99d05921026d1527331',
    ///     '0x0' // 0
    ///     ]
    ///     Returns
    ///     Transaction
    ///     Example
    ///     Request
    ///     curl -X POST --data
    ///     '{"jsonrpc":"2.0","method":"eth_getTransactionByBlockHashAndIndex","params":[0xc6ef2fc5426d6ad6fd9e2a26abeab0aa2411b7ab17f30a99d3cb96aed1d1055b,
    ///     "0x0"],"id":1}'
    ///     Result see eth_getTransactionByHash
    /// </Summary>
    public class EthGetTransactionByBlockHashAndIndex : RpcRequestResponseHandler<Transaction>
    {
        public EthGetTransactionByBlockHashAndIndex(IClient client)
            : base(client, ApiMethods.eth_getTransactionByBlockHashAndIndex.ToString())
        {
        }

        public Task<Transaction> SendRequestAsync(string blockHash, HexBigInteger transactionIndex,
            object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            if (transactionIndex == null) throw new ArgumentNullException(nameof(transactionIndex));
            return base.SendRequestAsync(id, blockHash.EnsureHexPrefix(), transactionIndex);
        }

        public RpcRequest BuildRequest(string blockHash, HexBigInteger transactionIndex, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            if (transactionIndex == null) throw new ArgumentNullException(nameof(transactionIndex));
            return base.BuildRequest(id, blockHash.EnsureHexPrefix(), transactionIndex);
        }
    }
}