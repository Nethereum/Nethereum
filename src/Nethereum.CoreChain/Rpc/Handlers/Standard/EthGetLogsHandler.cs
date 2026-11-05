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

            var result = await LogQueryGuards.GetGuardedLogsAsync(filter, context);
            await FilteredLogRpcMapper.FillBlockTimestampsAsync(result, context.Node);
            return Success(request.Id, result);
        }
    }
}
