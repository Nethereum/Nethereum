using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthNewBlockFilterHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_newBlockFilter.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var currentBlock = await context.Node.GetBlockNumberAsync();
            var filterId = context.Node.Filters.CreateBlockFilter(currentBlock);
            return Success(request.Id, filterId);
        }
    }
}
