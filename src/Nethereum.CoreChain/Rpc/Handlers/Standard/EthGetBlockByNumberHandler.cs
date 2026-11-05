using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            if (IsAboveExecutedHead(context, blockNumber))
            {
                return Success(request.Id, null);
            }

            var blockHeader = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);

            return Success(request.Id, await BlockRpcResponseBuilder.BuildAsync(context, blockHeader, blockHash, includeTransactions));
        }

        internal static bool IsAboveExecutedHead(RpcContext context, System.Numerics.BigInteger blockNumber)
        {
            var bundle = context.Services != null ? context.GetService<IChainStoreBundle>() : null;
            if (!(bundle is IAtomicBlockFlush)) return false;
            var executedHead = bundle.Metadata.GetLastBlock();
            return executedHead > 0 && blockNumber > executedHead;
        }
    }
}
