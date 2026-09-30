using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetUncleCountByBlockNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getUncleCountByBlockNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);
            if (blockHash == null)
            {
                return Success(request.Id, null);
            }

            var uncleStore = context.Node.Uncles;
            var uncles = uncleStore != null ? await uncleStore.GetByBlockNumberAsync(blockNumber) : null;
            return Success(request.Id, new HexBigInteger(uncles?.Count ?? 0));
        }
    }
}
