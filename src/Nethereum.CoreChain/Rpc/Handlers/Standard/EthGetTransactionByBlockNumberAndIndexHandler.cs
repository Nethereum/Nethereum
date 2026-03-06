using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionByBlockNumberAndIndexHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionByBlockNumberAndIndex.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var indexHex = GetParam<string>(request, 1);

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var index = (int)indexHex.HexToBigInteger(false);

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);
            if (blockHash == null)
                return Success(request.Id, null);

            var txStore = context.GetService<ITransactionStore>();
            if (txStore == null)
                return Success(request.Id, null);

            var txs = await txStore.GetByBlockHashAsync(blockHash);
            if (txs == null || index >= txs.Count)
                return Success(request.Id, null);

            var tx = txs[index];
            return Success(request.Id, tx.ToRpcTransaction(blockHash, blockNumber, index));
        }
    }
}
