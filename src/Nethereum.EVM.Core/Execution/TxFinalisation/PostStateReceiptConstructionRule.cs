using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// Pre-EIP-658 (Frontier through Tangerine Whistle, mainnet block 0
    /// to 4,370,000): receipt's first field is the 32-byte intermediate
    /// state root captured after the tx finalised.
    ///
    /// <para>When <paramref name="intermediatePostStateRoot"/> is non-null
    /// the rule produces a canonical post-state receipt (geth
    /// <c>core/types/receipt.go::statusEncoding</c> pre-Byzantium branch).
    /// When the caller hasn't captured the root yet — current state of
    /// <c>TransactionProcessor</c> / <c>BlockExecutor</c>, which run
    /// without per-tx <c>IStateRootCalculator</c> plumbing — the rule
    /// falls back to status-form so existing pre-Byzantium re-execution
    /// produces the same receipt shape it has historically produced.
    /// Receipts-root validation against canonical wire bytes will mismatch
    /// in that fallback mode; <c>HistoricalBlockBackfiller</c> already
    /// skips pre-Byzantium receipts-root checks for the same reason.</para>
    /// </summary>
    public sealed class PostStateReceiptConstructionRule : IReceiptConstructionRule
    {
        public static readonly PostStateReceiptConstructionRule Instance = new PostStateReceiptConstructionRule();
        private PostStateReceiptConstructionRule() { }

        public bool RequiresIntermediatePostStateRoot => true;

        public Receipt Construct(
            bool success,
            EvmUInt256 cumulativeGasUsed,
            byte[] bloom,
            List<Log> logs,
            byte[] intermediatePostStateRoot)
        {
            if (intermediatePostStateRoot == null)
                return Receipt.CreateStatusReceipt(success, cumulativeGasUsed, bloom, logs);
            return Receipt.CreatePostStateReceipt(intermediatePostStateRoot, cumulativeGasUsed, bloom, logs);
        }
    }
}
