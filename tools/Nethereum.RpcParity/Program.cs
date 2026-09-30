using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RpcParity.Groups;
using Nethereum.Web3;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Differential JSON-RPC tester: runs the same typed Nethereum call
    /// against a subject node (X) and a reference node (Y, e.g. geth) across
    /// a sample of blocks and deep-compares every response. The permanent
    /// oracle for follower RPC-parity fixes — see
    /// docs/internal/2026-08-20-follower-rpc-geth-parity-design.md.
    /// </summary>
    internal static class Program
    {
        public static async Task<int> Main(string[] argv)
        {
            CliOptions options;
            try
            {
                options = CliOptions.Parse(argv);
                options.Validate();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                PrintHelp();
                return 1;
            }

            if (options.SelfTest)
                return SelfTest.Run() ? 0 : 1;

            try
            {
                return await RunAsync(options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static async Task<int> RunAsync(CliOptions options)
        {
            var nodeX = new Web3.Web3(options.XUrl);
            var nodeY = new Web3.Web3(options.YUrl);

            var blocks = await BlockSampler.ResolveAsync(nodeX, nodeY, options).ConfigureAwait(false);
            Console.WriteLine($"rpc-parity x={options.XUrl} y={options.YUrl}");
            Console.WriteLine($"  blocks: [{string.Join(",", blocks)}]");
            Console.WriteLine($"  groups: [{string.Join(",", options.Groups)}]");
            Console.WriteLine($"  ignore (cosmetic, suppressed): [{string.Join(",", options.Ignore)}]");
            Console.WriteLine();

            var report = new ReportWriter();
            var runner = new ParityRunner(report, options.Ignore);
            var ctx = new ParityContext(nodeX, nodeY, blocks, runner);
            await PopulateReferenceBlocksAsync(ctx).ConfigureAwait(false);

            if (options.Groups.Contains("blocks")) await BlocksGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("tx")) await TxGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("receipts")) await ReceiptsGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("logs")) await LogsGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("state")) await StateGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("sim")) await SimGroup.RunAsync(ctx).ConfigureAwait(false);
            if (options.Groups.Contains("trace")) await TraceGroup.RunAsync(ctx).ConfigureAwait(false);

            var diffCount = report.PrintSummary();
            return diffCount == 0 ? 0 : 1;
        }

        // Fetched once from node Y (the reference node) so every group draws
        // the same first-tx hash / block hash / busy address for a given
        // sampled block, instead of each group re-resolving its own inputs.
        private static async Task PopulateReferenceBlocksAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                try
                {
                    var block = await ctx.NodeY.Eth.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(new BlockParameter(new HexBigInteger(n))).ConfigureAwait(false);
                    if (block != null)
                        ctx.ReferenceBlocks[n] = block;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"warn: reference block {n} unavailable on Y: {ex.Message}");
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Nethereum RpcParity — differential JSON-RPC tester (subject X vs reference Y)");
            Console.WriteLine();
            Console.WriteLine("usage:");
            Console.WriteLine("  nethereum-rpcparity --x <url> --y <url> [--from <blk>] [--to <blk>] [--blocks n1,n2,...]");
            Console.WriteLine("                       [--groups blocks,tx,receipts,logs,state,sim,trace] [--ignore field1,field2]");
            Console.WriteLine("  nethereum-rpcparity --selftest");
            Console.WriteLine();
            Console.WriteLine("options:");
            Console.WriteLine("  --x <url>       subject node JSON-RPC endpoint");
            Console.WriteLine("  --y <url>       reference node JSON-RPC endpoint (e.g. geth)");
            Console.WriteLine("  --from/--to     inclusive block range to sample (default: last ~10000 blocks up to the common tip)");
            Console.WriteLine("  --blocks        explicit comma-separated block list (overrides --from/--to sampling)");
            Console.WriteLine("  --groups        comma-separated subset of blocks,tx,receipts,logs,state,sim,trace (default: all)");
            Console.WriteLine("  --ignore        comma-separated DTO property names to suppress in the deep-compare (added to the default cosmetic set)");
            Console.WriteLine($"  --ignore-none   clear the default cosmetic ignore set ({string.Join(",", CliOptions.DefaultCosmeticIgnores)})");
            Console.WriteLine("  --selftest      run the comparison-engine self-test with no live node");
        }
    }
}
