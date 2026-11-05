using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetFilterChangesHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getFilterChanges.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var filterId = GetParam<string>(request, 0);
            var filterState = context.Node.Filters.GetFilter(filterId);

            if (filterState == null)
            {
                throw RpcException.InvalidParams("Filter not found");
            }

            var currentBlock = await context.Node.GetBlockNumberAsync();

            switch (filterState.Type)
            {
                case FilterType.Log:
                    return await HandleLogFilterAsync(request, context, filterState, currentBlock);

                case FilterType.Block:
                    return await HandleBlockFilterAsync(request, context, filterState, currentBlock);

                case FilterType.PendingTransaction:
                    return await HandlePendingTransactionFilterAsync(request, context, filterState);

                default:
                    throw RpcException.InvalidParams("Unknown filter type");
            }
        }

        private async Task<RpcResponseMessage> HandleLogFilterAsync(
            RpcRequestMessage request,
            RpcContext context,
            FilterState filterState,
            BigInteger currentBlock)
        {
            var installed = filterState.LogFilter ?? new LogFilter();
            LogFilterLimits.Enforce(installed, context.Node.Config.RpcLogQueryLimit);
            var filter = new LogFilter
            {
                FromBlock = filterState.LastCheckedBlock + 1,
                ToBlock = currentBlock,
                Addresses = installed.Addresses,
                Topics = installed.Topics
            };

            if (filter.FromBlock > currentBlock)
            {
                return Success(request.Id, new List<object>());
            }

            LogQueryGuards.EnforceBlockRangeCap(filter, context);

            var logs = await context.Node.Logs.GetLogsAsync(filter);
            LogQueryGuards.EnforceResultCap(logs.Count, context);
            context.Node.Filters.UpdateFilterLastBlock(filterState.Id, currentBlock);

            var result = logs.Select(FilteredLogRpcMapper.ToRpcFilterLog).ToList();
            return Success(request.Id, result);
        }

        private async Task<RpcResponseMessage> HandleBlockFilterAsync(
            RpcRequestMessage request,
            RpcContext context,
            FilterState filterState,
            BigInteger currentBlock)
        {
            var blockHashes = new List<string>();
            var startBlock = filterState.LastCheckedBlock + 1;

            for (var i = startBlock; i <= currentBlock; i++)
            {
                var blockHash = await context.Node.GetBlockHashByNumberAsync(i);
                if (blockHash != null)
                {
                    blockHashes.Add(blockHash.ToHex(true));
                }
            }

            context.Node.Filters.UpdateFilterLastBlock(filterState.Id, currentBlock);
            return Success(request.Id, blockHashes);
        }

        private async Task<RpcResponseMessage> HandlePendingTransactionFilterAsync(
            RpcRequestMessage request,
            RpcContext context,
            FilterState filterState)
        {
            var pendingTxs = await context.Node.GetPendingTransactionsAsync();
            var txHashes = pendingTxs?.Select(tx => tx.Hash.ToHex(true)).ToList() ?? new List<string>();
            return Success(request.Id, txHashes);
        }

    }
}
