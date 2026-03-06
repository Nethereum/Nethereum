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
            var filter = await ParseLogFilterAsync(filterInput, context);
            var logs = await context.Node.Logs.GetLogsAsync(filter);
            var result = logs.Select(ConvertToRpcLog).ToList();
            return Success(request.Id, result);
        }
    }
}
