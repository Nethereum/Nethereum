using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>
    /// getLogs coverage: a small block range, an address filter, a topic
    /// filter, and a by-blockHash lookup. Ranges are kept to 3 blocks to
    /// stay well under any node's result-size cap.
    /// </summary>
    internal static class LogsGroup
    {
        private const string Group = "logs";
        private const long RangeSpan = 2;

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                var from = new BlockParameter(new HexBigInteger(n));
                var to = new BlockParameter(new HexBigInteger(n + RangeSpan));

                await RunFilterAsync(ctx, "GetLogs/range", $"{n}..{n + RangeSpan}", new NewFilterInput
                {
                    FromBlock = from,
                    ToBlock = to
                }).ConfigureAwait(false);

                await RunFilterAsync(ctx, "GetLogs/address", $"{n}..{n + RangeSpan}+{Erc20Fixtures.UsdtAddress}", new NewFilterInput
                {
                    FromBlock = from,
                    ToBlock = to,
                    Address = new[] { Erc20Fixtures.UsdtAddress }
                }).ConfigureAwait(false);

                await RunFilterAsync(ctx, "GetLogs/topic", $"{n}..{n + RangeSpan}+Transfer", new NewFilterInput
                {
                    FromBlock = from,
                    ToBlock = to,
                    Address = new[] { Erc20Fixtures.UsdtAddress },
                    Topics = new object[] { Erc20Fixtures.TransferTopic }
                }).ConfigureAwait(false);

                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference) || reference.BlockHash == null)
                    continue;

                await RunByBlockHashAsync(ctx, reference.BlockHash).ConfigureAwait(false);
            }
        }

        private static Task RunFilterAsync(ParityContext ctx, string method, string input, NewFilterInput filter) =>
            ctx.Runner.RunAsync(Group, method, input,
                () => ctx.NodeX.Eth.Filters.GetLogs.SendRequestAsync(filter),
                () => ctx.NodeY.Eth.Filters.GetLogs.SendRequestAsync(filter));

        // NewFilterInput has no blockHash field (Nethereum's typed DTO only
        // models the fromBlock/toBlock form), so this one case goes raw.
        private static Task RunByBlockHashAsync(ParityContext ctx, string blockHash)
        {
            var filter = new JObject { ["blockHash"] = blockHash };
            return ctx.Runner.RunAsync(Group, "GetLogs/blockHash", blockHash,
                () => ctx.NodeX.Client.SendRequestAsync<FilterLog[]>(new RpcRequest(0, "eth_getLogs", filter)),
                () => ctx.NodeY.Client.SendRequestAsync<FilterLog[]>(new RpcRequest(0, "eth_getLogs", filter)));
        }
    }
}
