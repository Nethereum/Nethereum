using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthNewFilterHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_newFilter.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterInput = GetJsonElement(request, 0);
            var filter = await ParseLogFilterAsync(filterInput, context);
            var currentBlock = await context.Node.GetBlockNumberAsync();
            var filterId = context.Node.Filters.CreateLogFilter(filter, currentBlock);
            return Success(request.Id, filterId);
        }
    }
}
