using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>Block-shaped RPC coverage: by-number and by-hash, full transactions and hashes-only.</summary>
    internal static class BlocksGroup
    {
        private const string Group = "blocks";

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                var numberParam = new BlockParameter(new HexBigInteger(n));

                await ctx.Runner.RunAsync(Group, "GetBlockWithTransactionsByNumber", n.ToString(),
                    () => ctx.NodeX.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(numberParam),
                    () => ctx.NodeY.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(numberParam)).ConfigureAwait(false);

                await ctx.Runner.RunAsync(Group, "GetBlockWithTransactionsHashesByNumber", n.ToString(),
                    () => ctx.NodeX.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(numberParam),
                    () => ctx.NodeY.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(numberParam)).ConfigureAwait(false);

                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference) || reference.BlockHash == null)
                    continue;

                var hash = reference.BlockHash;

                await ctx.Runner.RunAsync(Group, "GetBlockWithTransactionsByHash", hash,
                    () => ctx.NodeX.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(hash),
                    () => ctx.NodeY.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(hash)).ConfigureAwait(false);

                await ctx.Runner.RunAsync(Group, "GetBlockWithTransactionsHashesByHash", hash,
                    () => ctx.NodeX.Eth.Blocks.GetBlockWithTransactionsHashesByHash.SendRequestAsync(hash),
                    () => ctx.NodeY.Eth.Blocks.GetBlockWithTransactionsHashesByHash.SendRequestAsync(hash)).ConfigureAwait(false);
            }
        }
    }
}
