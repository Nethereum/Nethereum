using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
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
            var logs = await context.Node.Logs.GetLogsAsync(filter);
            var result = logs.Select(ConvertToRpcLog).ToList();

            return Success(request.Id, result);
        }

        private object ConvertToRpcLog(FilteredLog log)
        {
            return new
            {
                address = log.Address,
                topics = log.Topics?.Select(t => t.ToHex(true)).ToList() ?? new List<string>(),
                data = log.Data?.ToHex(true) ?? "0x",
                blockNumber = ToHex(log.BlockNumber),
                transactionHash = log.TransactionHash?.ToHex(true),
                transactionIndex = ToHex(log.TransactionIndex),
                blockHash = log.BlockHash?.ToHex(true),
                logIndex = ToHex(log.LogIndex),
                removed = log.Removed
            };
        }
    }
}
