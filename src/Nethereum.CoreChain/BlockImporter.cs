using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Follower wrapper that drives one <see cref="BlockExecutor"/>
    /// invocation per imported block and persists the resulting block
    /// body. Wraps:
    ///   1. Reverse-diff journal bracketing
    ///      (<see cref="IHistoricalStateProvider.SetCurrentBlockNumber"/>
    ///      before execute / <see cref="IHistoricalStateProvider.ClearCurrentBlockNumberAsync"/>
    ///      in finally) — engine does not touch the journal; it is a
    ///      persistence concern owned by the caller.
    ///   2. Calls
    ///      <see cref="BlockExecutor.ExecuteAsync"/> with default options.
    ///   3. Computes the canonical block hash and persists header + uncles
    ///      + tx / receipt / log bodies via the wired stores.
    ///   4. Surfaces a state-root mismatch flag against
    ///      <c>header.StateRoot</c> on the returned result.
    ///
    /// Implements <see cref="Sync.IBlockExecutor"/> so existing
    /// <see cref="Sync.FollowerService"/> wiring keeps working. Returns
    /// <see cref="BlockImporterResult"/>, which carries the engine's
    /// <see cref="BlockExecutionResult"/> plus the persisted block hash.
    /// </summary>
    public sealed class BlockImporter : Nethereum.CoreChain.Sync.IBlockExecutor
    {
        private readonly BlockExecutor _engine;
        private readonly IBlockStore _blockStore;
        private readonly IStateStore _stateStore;
        private readonly ITransactionStore? _transactionStore;
        private readonly IReceiptStore? _receiptStore;
        private readonly ILogStore? _logStore;
        private readonly IUncleStore? _uncleStore;
        private readonly ILogger<BlockImporter>? _logger;
        private readonly Sha3Keccack _keccak = new();

        public BlockImporter(
            BlockExecutor engine,
            IBlockStore blockStore,
            IStateStore stateStore,
            ITransactionStore? transactionStore = null,
            IReceiptStore? receiptStore = null,
            ILogStore? logStore = null,
            IUncleStore? uncleStore = null,
            ILogger<BlockImporter>? logger = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _logStore = logStore;
            _uncleStore = uncleStore;
            _logger = logger;
        }

        /// <summary>
        /// Execute one block against the engine, validate the resulting
        /// post-state root against <c>header.StateRoot</c>, then persist
        /// header + uncles + tx / receipt / log bodies. Reverse-diff
        /// journal lifecycle is bracketed around the engine call when the
        /// wired state store is also an <see cref="IHistoricalStateProvider"/>;
        /// without this every state write would land in disk but no
        /// reverse-diff would be recorded, breaking rewind.
        /// </summary>
        public async Task<BlockImporterResult> ImportAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<WithdrawalEntry>? withdrawals,
            CancellationToken ct = default)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            // Reverse-diff journal lifecycle. When the wrapped state store is
            // a HistoricalStateStore (i.e. the host has wired the journal),
            // SetCurrentBlockNumber arms a fresh BlockJournal so every
            // SaveAccountAsync / SaveStorageAsync / DeleteAccountAsync /
            // ClearStorageAsync below records a pre-value. The matching
            // ClearCurrentBlockNumberAsync in the outer finally flushes the
            // collected diffs to IStateDiffStore (atomically per-store).
            var historyProvider = _stateStore as IHistoricalStateProvider;
            historyProvider?.SetCurrentBlockNumber((BigInteger)(ulong)header.BlockNumber);
            try
            {
                var entries = new List<TxEntry>(transactions?.Count ?? 0);
                if (transactions != null)
                {
                    foreach (var tx in transactions) entries.Add(new TxEntry(tx, null));
                }

                var result = await _engine.ExecuteAsync(
                    header,
                    entries,
                    uncles,
                    withdrawals,
                    new BlockExecutionOptions(),
                    ct).ConfigureAwait(false);

                if (result.Exception != null)
                {
                    return new BlockImporterResult
                    {
                        Fork = result.Fork,
                        ComputedStateRoot = result.PostStateRoot,
                        ExpectedStateRoot = header.StateRoot,
                        StateRootMismatch = result.StateRootMismatch,
                        TransactionsExecuted = result.Receipts.Count,
                        MinerRewardCredited = result.MinerRewardCredited,
                        WithdrawalsCredited = result.WithdrawalsCredited,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        ExecutionResults = result.Receipts
                    };
                }

                // Persist the block header + hash so subsequent blocks'
                // BLOCKHASH opcode can resolve it via IStateReader. Without
                // this, BLOCKHASH(N-1) reads zero and contracts that branch
                // on the parent hash compute the wrong result.
                var blockHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                await _blockStore.SaveAsync(header, blockHash).ConfigureAwait(false);

                // 6b. Persist uncles so re-execution from local stores can
                // reconstruct miner reward + uncle inclusion bonus without
                // re-fetching block bodies from peers. Stored as an RLP list
                // of uncle headers keyed by block hash. Save unconditionally
                // when the store is wired (even empty list — see store
                // contract) so downstream re-exec can distinguish "block had
                // no uncles" from "uncles weren't persisted".
                if (_uncleStore != null)
                {
                    await _uncleStore.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()).ConfigureAwait(false);
                }

                // 7. Persist transaction + receipt + log bodies when archive
                // stores are wired. Without this, audit replay /
                // eth_getTransactionByHash / eth_getLogs /
                // debug_traceTransaction can't be served without re-fetching
                // from peers. Optional so call sites that only care about
                // state transition (tests, smoke tools) pay no cost.
                //
                // effectiveGasPrice comes from txResult.EffectiveGasPrice
                // which TransactionProcessor stamps from
                // txData.GetEffectiveGasPrice(baseFee) — correct for legacy,
                // EIP-1559, EIP-4844, EIP-7702.
                if (_transactionStore != null && transactions != null && result.Receipts.Count == transactions.Count)
                {
                    for (int i = 0; i < transactions.Count; i++)
                    {
                        var tx = transactions[i];
                        await _transactionStore.SaveAsync(tx, blockHash, i, header.BlockNumber).ConfigureAwait(false);

                        var er = result.Receipts[i];
                        if (er == null || er.TransactionHash == null) continue;

                        if (_receiptStore != null && er.Receipt != null)
                        {
                            await _receiptStore.SaveAsync(
                                er.Receipt,
                                er.TransactionHash,
                                blockHash,
                                header.BlockNumber,
                                i,
                                er.GasUsed,
                                er.ContractAddress,
                                er.EffectiveGasPrice).ConfigureAwait(false);
                        }

                        if (_logStore != null && er.Logs != null && er.Logs.Count > 0)
                        {
                            await _logStore.SaveLogsAsync(
                                er.Logs,
                                er.TransactionHash,
                                blockHash,
                                header.BlockNumber,
                                i).ConfigureAwait(false);
                        }
                    }
                }

                return new BlockImporterResult
                {
                    Fork = result.Fork,
                    ComputedStateRoot = result.PostStateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = result.StateRootMismatch,
                    TransactionsExecuted = result.Receipts.Count,
                    MinerRewardCredited = result.MinerRewardCredited,
                    WithdrawalsCredited = result.WithdrawalsCredited,
                    BlockHash = blockHash,
                    ExecutionResults = result.Receipts
                };
            }
            finally
            {
                if (historyProvider != null)
                {
                    await historyProvider.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
            }
        }

        // IBlockExecutor entry point. Returns the same BlockImporterResult
        // shape that the rich ImportAsync API uses — no projection.
        Task<BlockImporterResult> Nethereum.CoreChain.Sync.IBlockExecutor.ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct)
            => ImportAsync(header, transactions, uncles, withdrawals, ct);
    }

    /// <summary>
    /// Output of one <see cref="BlockImporter.ImportAsync"/> call. Carries
    /// the engine's <see cref="BlockExecutionResult"/> summary plus the
    /// canonical block hash that was persisted, so callers building
    /// chain-tip metadata don't recompute the hash.
    /// </summary>
    public sealed class BlockImporterResult
    {
        public Nethereum.EVM.HardforkName Fork { get; init; }
        public byte[]? ComputedStateRoot { get; init; }
        public byte[]? ExpectedStateRoot { get; init; }
        public bool StateRootMismatch { get; init; }
        public int TransactionsExecuted { get; init; }
        public BigInteger MinerRewardCredited { get; init; }
        public int WithdrawalsCredited { get; init; }
        public byte[]? BlockHash { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }
        public IReadOnlyList<TransactionExecutionResult> ExecutionResults { get; init; } = Array.Empty<TransactionExecutionResult>();

        public bool RootMatches => !StateRootMismatch
                                   && ComputedStateRoot != null
                                   && ExpectedStateRoot != null
                                   && ByteUtil.AreEqual(ComputedStateRoot, ExpectedStateRoot);
    }
}
