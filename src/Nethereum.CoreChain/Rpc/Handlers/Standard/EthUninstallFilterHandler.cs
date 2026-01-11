using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthUninstallFilterHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_uninstallFilter.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterId = GetParam<string>(request, 0);
            var removed = context.Node.Filters.RemoveFilter(filterId);
            return Task.FromResult(Success(request.Id, removed));
        }
    }
}
