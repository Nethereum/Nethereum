using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Rsk.RPC.RskEth.DTOs;

namespace Nethereum.Rsk.RPC.RskEth
{
    public class RskEthGetBlockWithTransactionsHashesByHash : RpcRequestResponseHandler<RskBlockWithTransactionHashes>, IRskEthGetBlockWithTransactionsHashesByHash
    {
        public RskEthGetBlockWithTransactionsHashesByHash(IClient client)
            : base(client, ApiMethods.eth_getBlockByHash.ToString())
        {
        }

        public Task<RskBlockWithTransactionHashes> SendRequestAsync(string blockHash, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            return base.SendRequestAsync(id, blockHash.EnsureHexPrefix(), false);
        }

        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            return base.BuildRequest(id, blockHash.EnsureHexPrefix(), false);
        }
    }
}