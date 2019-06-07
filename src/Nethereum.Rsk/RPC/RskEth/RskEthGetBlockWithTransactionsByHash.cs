using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Rsk.RPC.RskEth.DTOs;

namespace Nethereum.Rsk.RPC.RskEth
{
    public class RskEthGetBlockWithTransactionsByHash : RpcRequestResponseHandler<RskBlockWithTransactions>, IRskEthGetBlockWithTransactionsByHash
    {
        public RskEthGetBlockWithTransactionsByHash(IClient client)
            : base(client, ApiMethods.eth_getBlockByHash.ToString())
        {
        }

        public Task<RskBlockWithTransactions> SendRequestAsync(string blockHash, object id = null)
        {
            return base.SendRequestAsync(id, blockHash.EnsureHexPrefix(), true);
        }

        public RpcRequest BuildRequest(string blockHash, object id = null)
        {
            return base.BuildRequest(id, blockHash.EnsureHexPrefix(), true);
        }
    }
}