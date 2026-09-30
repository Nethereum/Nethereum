using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.Gas;

namespace Nethereum.CoreChain.Rpc
{
    public static class RpcContextGasRules
    {
        public static async Task<IntrinsicGasRules> ResolveGasRulesAtBlockOrHeadAsync(
            this RpcContext context, BigInteger blockNumber)
        {
            var pricedAt = await context.Node.GetBlockByNumberAsync(blockNumber)
                ?? await context.Node.GetLatestBlockAsync();

            return context.Node.Config
                .GetHardforkConfigAt(
                    (long)(pricedAt?.BlockNumber ?? default),
                    (ulong)(pricedAt?.Timestamp ?? 0))
                .IntrinsicGasRules;
        }
    }
}
