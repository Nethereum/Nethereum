using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Rpc
{
    internal static class LogQueryGuards
    {
        public static async Task<BigInteger> ResolveConcreteRangeAsync(LogFilter filter, RpcContext context)
        {
            var latest = await context.Node.GetBlockNumberAsync();
            filter.FromBlock ??= latest;
            filter.ToBlock ??= latest;

            if (filter.FromBlock.Value > filter.ToBlock.Value)
            {
                throw RpcException.InvalidParams(
                    $"invalid block range params: fromBlock ({filter.FromBlock.Value}) is after toBlock ({filter.ToBlock.Value})");
            }

            return latest;
        }

        public static void EnforceToBlockWithinHead(LogFilter filter, BigInteger latest)
        {
            if (filter.ToBlock.Value > latest)
            {
                throw RpcException.InvalidParams("block range extends beyond current head block");
            }
        }

        public static void EnforceBlockRangeCap(LogFilter filter, RpcContext context)
        {
            var maxRange = context.Node.Config.RpcMaxLogBlockRange;
            if (maxRange <= 0) return;

            var range = filter.ToBlock.Value - filter.FromBlock.Value + 1;
            if (range > maxRange)
            {
                throw new RpcException(-32000,
                    $"requested too many blocks; range is {range}, max is {maxRange}");
            }
        }

        public static void EnforceResultCap(int resultCount, RpcContext context)
        {
            var maxResults = context.Node.Config.RpcMaxLogResults;
            if (maxResults <= 0) return;

            if (resultCount > maxResults)
            {
                throw new RpcException(-32000, $"query returned more than {maxResults} results");
            }
        }
    }
}
