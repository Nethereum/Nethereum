using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>
    /// debug_trace* coverage. Nethereum has no typed client for either
    /// method, so both go through the raw JSON-RPC client and are compared
    /// as normalized <see cref="JToken"/> trees via <see cref="JTokenComparer"/>.
    /// </summary>
    internal static class TraceGroup
    {
        private const string Group = "trace";

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference) || reference.Transactions.Length == 0)
                    continue;

                var txHash = reference.Transactions[0].TransactionHash;
                await ctx.Runner.RunTraceAsync(Group, "debug_traceTransaction", txHash,
                    () => ctx.NodeX.Client.SendRequestAsync<JToken>(new RpcRequest(0, "debug_traceTransaction", txHash)),
                    () => ctx.NodeY.Client.SendRequestAsync<JToken>(new RpcRequest(0, "debug_traceTransaction", txHash))).ConfigureAwait(false);

                var callObject = new JObject
                {
                    ["from"] = reference.Transactions[0].From,
                    ["to"] = Erc20Fixtures.UsdtAddress,
                    ["data"] = Erc20Fixtures.TotalSupplySelector
                };
                var blockTag = "0x" + n.ToString("x");

                await ctx.Runner.RunTraceAsync(Group, "debug_traceCall", $"{Erc20Fixtures.UsdtAddress}@{n}",
                    () => ctx.NodeX.Client.SendRequestAsync<JToken>(new RpcRequest(0, "debug_traceCall", callObject, blockTag)),
                    () => ctx.NodeY.Client.SendRequestAsync<JToken>(new RpcRequest(0, "debug_traceCall", callObject, blockTag))).ConfigureAwait(false);
            }
        }
    }
}
