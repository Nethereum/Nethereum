using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// Per-fork rule that builds a <see cref="Receipt"/> from a tx's
    /// execution result. Encodes the consensus difference between
    /// pre-EIP-658 forks (Frontier..Tangerine Whistle) which carry the
    /// 32-byte intermediate post-state root in <see cref="Receipt.PostStateOrStatus"/>,
    /// and Byzantium-onward forks (EIP-658) which carry a 1-byte status
    /// (0x01 success / 0x00 failure).
    ///
    /// <para>Mirrors geth's <c>core/state_processor.go:155-159</c> branch
    /// on <c>chainRules.IsByzantium</c>: pre-Byzantium computes the
    /// intermediate state root and stamps it on the receipt; post-Byzantium
    /// leaves it null and lets <c>statusEncoding()</c> emit the status byte.</para>
    /// </summary>
    public interface IReceiptConstructionRule
    {
        /// <summary>
        /// <c>true</c> if this rule needs the per-tx intermediate post-state
        /// root captured during execution. When <c>true</c>, the executor
        /// MUST compute the intermediate state root after the tx's balance
        /// updates and gas refund have been applied (but before the next tx
        /// executes) and pass it to <see cref="Construct"/>. When
        /// <c>false</c>, the executor may pass <c>null</c>.
        /// </summary>
        bool RequiresIntermediatePostStateRoot { get; }

        /// <summary>
        /// Build the receipt for a finished tx. <paramref name="intermediatePostStateRoot"/>
        /// is the 32-byte state root captured after the tx finalised, or
        /// <c>null</c> when <see cref="RequiresIntermediatePostStateRoot"/>
        /// is <c>false</c>.
        /// </summary>
        Receipt Construct(
            bool success,
            EvmUInt256 cumulativeGasUsed,
            byte[] bloom,
            List<Log> logs,
            byte[] intermediatePostStateRoot);
    }
}
