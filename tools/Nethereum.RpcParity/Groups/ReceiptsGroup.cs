using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RpcParity.Groups
{
    /// <summary>Receipt-shaped RPC coverage: single receipt by hash, and the whole-block receipts list.</summary>
    internal static class ReceiptsGroup
    {
        private const string Group = "receipts";

        public static async Task RunAsync(ParityContext ctx)
        {
            foreach (var n in ctx.Blocks)
            {
                if (!ctx.ReferenceBlocks.TryGetValue(n, out var reference))
                    continue;

                if (reference.Transactions.Length > 0)
                {
                    var hash = reference.Transactions[0].TransactionHash;
                    await ctx.Runner.RunAsync(Group, "GetTransactionReceipt", hash,
                        () => ctx.NodeX.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash),
                        () => ctx.NodeY.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash)).ConfigureAwait(false);
                }

                var numberHex = new HexBigInteger(n);
                await ctx.Runner.RunAsync(Group, "GetBlockReceiptsByNumber", n.ToString(),
                    () => ctx.NodeX.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(numberHex),
                    () => ctx.NodeY.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(numberHex)).ConfigureAwait(false);
            }
        }
    }
}
