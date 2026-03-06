using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionByBlockHashAndIndexHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionByBlockHashAndIndex.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var indexHex = GetParam<string>(request, 1);

            var blockHash = hashHex.HexToByteArray();
            var index = (int)indexHex.HexToBigInteger(false);

            var blockHeader = await context.Node.GetBlockByHashAsync(blockHash);
            if (blockHeader == null)
                return Success(request.Id, null);

            var txStore = context.GetService<ITransactionStore>();
            if (txStore == null)
                return Success(request.Id, null);

            var txs = await txStore.GetByBlockHashAsync(blockHash);
            if (txs == null || index >= txs.Count)
                return Success(request.Id, null);

            var tx = txs[index];
            return Success(request.Id, tx.ToRpcTransaction(blockHash, blockHeader.BlockNumber, index));
        }
    }
}
