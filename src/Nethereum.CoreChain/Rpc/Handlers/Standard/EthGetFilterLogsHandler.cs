using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetFilterLogsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getFilterLogs.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterId = GetParam<string>(request, 0);
            var filterState = context.Node.Filters.GetFilter(filterId);

            if (filterState == null)
            {
                throw RpcException.InvalidParams("Filter not found");
            }

            if (filterState.Type != FilterType.Log)
            {
                throw RpcException.InvalidParams("Filter is not a log filter");
            }

            var filter = filterState.LogFilter ?? new LogFilter();
            LogFilterLimits.Enforce(filter, context.Node.Config.RpcLogQueryLimit);

            var result = await LogQueryGuards.GetGuardedLogsAsync(filter, context);

            return Success(request.Id, result);
        }
    }
}
