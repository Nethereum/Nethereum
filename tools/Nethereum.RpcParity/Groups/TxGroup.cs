using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>Transaction-shaped RPC coverage: by-hash, by-block-position, and nonce reads.</summary>
    internal static class TxGroup
    {
        private const string Group = "tx";

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference) || reference.Transactions.Length == 0)
                    continue;

                var firstTx = reference.Transactions[0];
                var hash = firstTx.TransactionHash;

                await ctx.Runner.RunAsync(Group, "GetTransactionByHash", hash,
                    () => ctx.NodeX.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash),
                    () => ctx.NodeY.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash)).ConfigureAwait(false);

                var blockNumberHex = new HexBigInteger(n);
                var indexHex = new HexBigInteger(0);
                await ctx.Runner.RunAsync(Group, "GetTransactionByBlockNumberAndIndex", $"{n}/0",
                    () => ctx.NodeX.Eth.Transactions.GetTransactionByBlockNumberAndIndex.SendRequestAsync(blockNumberHex, indexHex),
                    () => ctx.NodeY.Eth.Transactions.GetTransactionByBlockNumberAndIndex.SendRequestAsync(blockNumberHex, indexHex)).ConfigureAwait(false);

                // The block's own first-tx sender is a guaranteed-active address at this block.
                var busyAddress = firstTx.From;
                var blockParam = new BlockParameter(new HexBigInteger(n));
                await ctx.Runner.RunAsync(Group, "GetTransactionCount", $"{busyAddress}@{n}",
                    () => ctx.NodeX.Eth.Transactions.GetTransactionCount.SendRequestAsync(busyAddress, blockParam),
                    () => ctx.NodeY.Eth.Transactions.GetTransactionCount.SendRequestAsync(busyAddress, blockParam)).ConfigureAwait(false);
            }
        }
    }
}
