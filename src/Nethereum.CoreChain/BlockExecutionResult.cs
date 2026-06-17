using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Canonical output of one <see cref="BlockExecutor.ExecuteAsync"/>
    /// invocation. Contains everything the caller wrappers
    /// (<see cref="BlockImporter"/>, <c>BlockProducer</c>, witness capture)
    /// need to validate, persist, and report: pre/post state roots,
    /// per-tx execution outputs, receipts + logs + block bloom, witness
    /// bytes if requested, and any error / state-root divergence info.
    /// </summary>
    public sealed class BlockExecutionResult
    {
        /// <summary>Hardfork the engine resolved at the input header's (number, timestamp).</summary>
        public required HardforkName Fork { get; init; }

        /// <summary>
        /// Parent header's <c>StateRoot</c> as read from the block store
        /// before execution. <c>null</c> only when the input header is
        /// genesis or its parent is not yet stored (cold AppChain init).
        /// </summary>
        public required byte[]? PreStateRoot { get; init; }

        /// <summary>
        /// State root after all state transitions: sys-calls + DAO drain +
        /// tx loop + withdrawals + rewards. <c>null</c> when
        /// <see cref="BlockExecutionOptions.ReadOnly"/> is set (the engine
        /// skips the final compute to avoid the calculator's trie-node
        /// writes).
        /// </summary>
        public required byte[]? PostStateRoot { get; init; }

        /// <summary>Per-tx full <see cref="TransactionExecutionResult"/> in input order.</summary>
        public required IReadOnlyList<TransactionExecutionResult> Receipts { get; init; }

        /// <summary>Flattened logs across all included txs (per-tx receipts also carry their own).</summary>
        public required IReadOnlyList<Log> Logs { get; init; }

        /// <summary>OR-combined LogsBloom across all included tx receipts. Null when no logs.</summary>
        public byte[]? BlockBloom { get; init; }

        /// <summary>
        /// Serialised <see cref="EVM.Witness.BinaryBlockWitness"/> when
        /// <see cref="BlockExecutionOptions.CaptureWitness"/> was set.
        /// </summary>
        public byte[]? WitnessBytes { get; init; }

        /// <summary>Total wei minted to <c>header.Coinbase</c> by the reward policy (pre-Merge mainnet only).</summary>
        public BigInteger MinerRewardCredited { get; init; }

        /// <summary>Withdrawals applied to state (Shanghai+).</summary>
        public int WithdrawalsCredited { get; init; }

        /// <summary>
        /// <c>true</c> when execution ran to completion but
        /// <see cref="PostStateRoot"/> diverged from the input
        /// <c>header.StateRoot</c>. Distinct from
        /// <see cref="Exception"/> — a mismatch means the EVM ran every tx
        /// and the post-state is well-formed but disagrees with what the
        /// network expects. Callers usually treat this as a consensus bug
        /// to diagnose, not a runtime error to retry. Set by the engine
        /// after the post-state root compute, by comparing the result
        /// against the input <c>header.StateRoot</c>; wrappers consume the
        /// flag but do not recompute it.
        /// </summary>
        public bool StateRootMismatch { get; init; }

        /// <summary>Hard execution failure (storage corruption, malformed tx, EVM crash). Null on success or pure state-root mismatch.</summary>
        public Exception? Exception { get; init; }

        /// <summary>Human-readable summary of <see cref="Exception"/> for log lines / metrics labels.</summary>
        public string? ErrorMessage { get; init; }
    }
}
