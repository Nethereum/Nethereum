using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetLogsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getLogs.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterInput = GetJsonElement(request, 0);
            var filter = await LogFilterParser.ParseAsync(filterInput, context);

            var latest = await LogQueryGuards.ResolveConcreteRangeAsync(filter, context);
            LogQueryGuards.EnforceBlockRangeCap(filter, context);
            LogQueryGuards.EnforceToBlockWithinHead(filter, latest);

            var logs = await context.Node.Logs.GetLogsAsync(filter);
            LogQueryGuards.EnforceResultCap(logs.Count, context);

            var result = logs.Select(FilteredLogRpcMapper.ToRpcFilterLog).ToList();
            await FilteredLogRpcMapper.FillBlockTimestampsAsync(result, context.Node);
            return Success(request.Id, result);
        }
    }
}
