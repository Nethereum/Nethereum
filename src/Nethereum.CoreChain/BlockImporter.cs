using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public sealed class BlockImporter : Nethereum.CoreChain.Sync.IBlockExecutor
    {
        private readonly BlockExecutor _engine;
        private readonly IBlockStore _blockStore;
        private readonly IStateStore _stateStore;
        private readonly ITransactionStore? _transactionStore;
        private readonly IReceiptStore? _receiptStore;
        private readonly ILogStore? _logStore;
        private readonly IUncleStore? _uncleStore;
        private readonly IBlockAccessListStore? _blockAccessListStore;
        private readonly IWithdrawalStore? _withdrawalStore;
        private readonly ILogger<BlockImporter>? _logger;
        private readonly NodeCommitBlockContext? _nodeCommitBlockContext;
        private readonly IAtomicBlockFlush? _atomicFlush;
        private readonly IFlushCadence _flushCadence;

        public BlockImporter(
            BlockExecutor engine,
            IBlockStore blockStore,
            IStateStore stateStore,
            ITransactionStore? transactionStore = null,
            IReceiptStore? receiptStore = null,
            ILogStore? logStore = null,
            IUncleStore? uncleStore = null,
            ILogger<BlockImporter>? logger = null,
            NodeCommitBlockContext? nodeCommitBlockContext = null,
            IAtomicBlockFlush? atomicFlush = null,
            IFlushCadence? flushCadence = null,
            IWithdrawalStore? withdrawalStore = null)
            : this(engine, blockStore, stateStore, transactionStore, receiptStore, logStore, uncleStore,
                   logger, nodeCommitBlockContext, atomicFlush, flushCadence, blockAccessListStore: null,
                   withdrawalStore: withdrawalStore)
        {
        }

        public BlockImporter(
            BlockExecutor engine,
            IBlockStore blockStore,
            IStateStore stateStore,
            ITransactionStore? transactionStore,
            IReceiptStore? receiptStore,
            ILogStore? logStore,
            IUncleStore? uncleStore,
            ILogger<BlockImporter>? logger,
            NodeCommitBlockContext? nodeCommitBlockContext,
            IAtomicBlockFlush? atomicFlush,
            IFlushCadence? flushCadence,
            IBlockAccessListStore? blockAccessListStore,
            IWithdrawalStore? withdrawalStore = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _logStore = logStore;
            _uncleStore = uncleStore;
            _blockAccessListStore = blockAccessListStore;
            _withdrawalStore = withdrawalStore;
            _logger = logger;
            _nodeCommitBlockContext = nodeCommitBlockContext;
            _atomicFlush = atomicFlush;
            _flushCadence = flushCadence ?? new EveryBlockFlushCadence();
            RefuseArmedBracketWithoutFlush(nodeCommitBlockContext, atomicFlush);
            RefuseWindowedCadenceOverNonHistoricalStore(_flushCadence, stateStore);
        }

        public Task<BlockImporterResult> ImportAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            CancellationToken ct = default) =>
            ImportAsync(header, transactions, uncles, withdrawals, declaredBlockAccessList: null, ct);

        /// <summary>
        /// Imports a block whose access list the caller already holds - a payload from the
        /// Engine API, a body from eth/71. EIP-7928 §Engine API: engine_newPayloadV5 "Returns
        /// INVALID if access list is malformed or doesn't match". Recomputing the list answers
        /// "doesn't match"; the declared list is the only thing that can answer "malformed".
        /// An overload rather than an optional parameter, because this ships as a package and a
        /// defaulted argument would break binary compatibility for existing callers.
        /// </summary>
        public async Task<BlockImporterResult> ImportAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<Nethereum.Model.AccountChanges>? declaredBlockAccessList,
            CancellationToken ct = default)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var historyProvider = _stateStore as IHistoricalStateProvider;
            historyProvider?.SetCurrentBlockNumber((BigInteger)(ulong)header.BlockNumber);
            bool rootMatched = false;
            byte[]? blockHash = null;
            _nodeCommitBlockContext?.Arm((ulong)header.BlockNumber);
            try
            {
                var entries = BuildTransactionEntries(transactions);

                var result = await _engine.ExecuteAsync(
                    header,
                    entries,
                    uncles,
                    WithdrawalAdapter.Convert(withdrawals),
                    new BlockExecutionOptions
                    {
                        Role = BlockExecutionRole.Validating,
                        DeclaredBlockAccessList = declaredBlockAccessList
                    },
                    ct).ConfigureAwait(false);

                if (ExecutionEarnedNoPersistence(result))
                {
                    return BlockImporterResult.From(result, header.StateRoot, blockHash: null);
                }

                blockHash = BlockHashCalculator.ForFork(header, result.Fork);
                await PersistBlockAndArtifactsAsync(header, result, uncles, withdrawals, transactions, blockHash).ConfigureAwait(false);

                rootMatched = true;
                return BlockImporterResult.From(result, header.StateRoot, blockHash);
            }
            finally
            {
                _nodeCommitBlockContext?.Clear();
                if (rootMatched)
                {
                    await FlushAndJournalMatchedBlockAsync(historyProvider, header, blockHash!).ConfigureAwait(false);
                }
                else
                {
                    await DiscardAndRevertUnpersistedBlockAsync(historyProvider).ConfigureAwait(false);
                }
            }
        }

        private static List<TxEntry> BuildTransactionEntries(IList<ISignedTransaction>? transactions)
        {
            var entries = new List<TxEntry>(transactions?.Count ?? 0);
            if (transactions != null)
            {
                foreach (var tx in transactions) entries.Add(new TxEntry(tx, null));
            }
            return entries;
        }

        private static bool ExecutionEarnedNoPersistence(BlockExecutionResult result)
            => result.Exception != null || result.ExecutionValidityMismatch;

        private async Task PersistBlockAndArtifactsAsync(
            BlockHeader header,
            BlockExecutionResult result,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            IList<ISignedTransaction>? transactions,
            byte[] blockHash)
        {
            await _blockStore.SaveAsync(header, blockHash).ConfigureAwait(false);

            if (_uncleStore != null)
            {
                await _uncleStore.SaveAsync(blockHash, uncles ?? new List<BlockHeader>()).ConfigureAwait(false);
            }

            await PersistWithdrawalsAsync(header, blockHash, withdrawals).ConfigureAwait(false);

            await PersistBlockAccessListAsync(result, blockHash).ConfigureAwait(false);

            await PersistTransactionArtifactsAsync(transactions, result, blockHash, header.BlockNumber).ConfigureAwait(false);
        }

        private Task PersistWithdrawalsAsync(BlockHeader header, byte[] blockHash, IList<Withdrawal>? withdrawals)
        {
            if (_withdrawalStore == null || header.WithdrawalsRoot == null) return Task.CompletedTask;
            return _withdrawalStore.SaveAsync(blockHash, withdrawals ?? new List<Withdrawal>());
        }

        private async Task FlushWindowAtBoundaryAsync(
            IAtomicBlockFlush atomicFlush, IHistoricalStateProvider? historyProvider,
            BlockHeader header, byte[] blockHash)
        {
            if (historyProvider != null)
            {
                await historyProvider.RecordBlockDiffAsync().ConfigureAwait(false);
            }
            var flat = historyProvider != null
                ? await historyProvider.CaptureBufferAsync().ConfigureAwait(false)
                : null;
            var swFlush = System.Diagnostics.Stopwatch.StartNew();
            await atomicFlush.FlushBlockAsync(flat, (ulong)header.BlockNumber, blockHash).ConfigureAwait(false);
            swFlush.Stop();
            _logger?.LogInformation(
                "state.flush.boundary block={Block} flush_ms={FlushMs} cadence_K={K}",
                header.BlockNumber, swFlush.ElapsedMilliseconds,
                _flushCadence is FixedIntervalFlushCadence fic ? (long)fic.K : 1L);
        }

        private async Task RecordOrClearBlockJournalAsync(
            IHistoricalStateProvider? historyProvider, bool boundary)
        {
            if (historyProvider != null)
            {
                if (_atomicFlush != null && !boundary)
                {
                    await historyProvider.RecordBlockDiffAsync().ConfigureAwait(false);
                }
                else
                {
                    await historyProvider.ClearCurrentBlockNumberAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task DiscardAndRevertUnpersistedBlockAsync(IHistoricalStateProvider? historyProvider)
        {
            _atomicFlush?.DiscardCapturedBlock();
            if (historyProvider != null)
            {
                await historyProvider.RevertCurrentBlockAsync().ConfigureAwait(false);
            }
        }

        private async Task FlushAndJournalMatchedBlockAsync(
            IHistoricalStateProvider? historyProvider, BlockHeader header, byte[] blockHash)
        {
            bool boundary = _flushCadence.IsBoundary((ulong)header.BlockNumber);
            if (_atomicFlush != null)
            {
                if (boundary)
                {
                    await FlushWindowAtBoundaryAsync(_atomicFlush, historyProvider, header, blockHash).ConfigureAwait(false);
                }
            }
            await RecordOrClearBlockJournalAsync(historyProvider, boundary).ConfigureAwait(false);
        }

        private static void RefuseArmedBracketWithoutFlush(
            NodeCommitBlockContext? nodeCommitBlockContext, IAtomicBlockFlush? atomicFlush)
        {
            if (nodeCommitBlockContext != null && atomicFlush == null)
                throw new ArgumentException(
                    "nodeCommitBlockContext is non-null but atomicFlush is null: an armed node-history bracket " +
                    "requires atomicFlush to drain the deferred trie-node capture, or armed blocks' trie writes " +
                    "are silently lost. Pass atomicFlush (typically the same bundle cast to IAtomicBlockFlush).",
                    nameof(atomicFlush));
        }

        private static void RefuseWindowedCadenceOverNonHistoricalStore(
            IFlushCadence flushCadence, IStateStore stateStore)
        {
            bool cadenceSafeOverNonHistorical = flushCadence is EveryBlockFlushCadence
                || (flushCadence is FixedIntervalFlushCadence k1Cadence && k1Cadence.K == 1);
            if (!cadenceSafeOverNonHistorical && stateStore is not IHistoricalStateProvider)
                throw new ArgumentException(
                    $"flushCadence ({flushCadence.GetType().Name}) may produce a non-every-block boundary, but " +
                    $"stateStore ({stateStore.GetType().Name}) is not an IHistoricalStateProvider: a real window " +
                    "needs the buffered flat overlay a historical state store provides, or non-boundary blocks' " +
                    "flat writes run ahead of the durable cursor with no buffered pre-image to recover from on a " +
                    "crash. Wire an IHistoricalStateProvider state store (e.g. HistoricalStateStore), or use a " +
                    "K=1 cadence (EveryBlockFlushCadence / FixedIntervalFlushCadence(1)).",
                    nameof(flushCadence));
        }

        private Task PersistBlockAccessListAsync(BlockExecutionResult result, byte[] blockHash)
        {
            if (_blockAccessListStore == null || result.BlockAccessList == null) return Task.CompletedTask;
            return _blockAccessListStore.SaveAsync(
                blockHash, BlockAccessListRLPEncoder.Current.Encode(result.BlockAccessList));
        }

        private async Task PersistTransactionArtifactsAsync(
            IList<ISignedTransaction>? transactions, BlockExecutionResult result, byte[] blockHash, BigInteger blockNumber)
        {
            if (!(_transactionStore != null && transactions != null && result.Receipts.Count == transactions.Count))
                return;

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                await _transactionStore.SaveAsync(tx, blockHash, i, blockNumber).ConfigureAwait(false);

                var er = result.Receipts[i];
                if (er == null || er.TransactionHash == null) continue;

                if (_receiptStore != null && er.Receipt != null)
                {
                    await _receiptStore.SaveAsync(
                        er.Receipt,
                        er.TransactionHash,
                        blockHash,
                        blockNumber,
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
                        blockNumber,
                        i).ConfigureAwait(false);
                }
            }
        }

        Task<BlockImporterResult> Nethereum.CoreChain.Sync.IBlockExecutor.ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals,
            CancellationToken ct)
            => ImportAsync(header, transactions, uncles, withdrawals, ct);
    }

    public sealed class BlockImporterResult : IBlockValidityChecks
    {
        public Nethereum.EVM.HardforkName Fork { get; init; }
        public byte[]? ComputedStateRoot { get; init; }
        public byte[]? ExpectedStateRoot { get; init; }
        public bool StateRootMismatch { get; init; }

        public bool RequestsHashMismatch { get; init; }

        public bool ReceiptsRootMismatch { get; init; }

        public bool LogsBloomMismatch { get; init; }

        public bool GasUsedMismatch { get; init; }

        public bool BlobGasUsedMismatch { get; init; }

        public bool ExcessBlobGasMismatch { get; init; }

        public bool BlobFieldFormatMismatch { get; init; }

        public bool BaseFeeMismatch { get; init; }

        public bool GasCapacityExceeded { get; init; }

        public bool BlobGasCapacityExceeded { get; init; }

        public bool BlockAccessListHashMismatch { get; init; }

        public bool BlockAccessListGasLimitExceeded { get; init; }

        public bool BlockAccessListMalformed { get; init; }

        public bool ContainsInvalidTransaction { get; init; }

        public bool GasLimitBoundViolated { get; init; }

        public bool WithdrawalsRootMismatch { get; init; }

        public List<AccountChanges>? BlockAccessList { get; init; }

        public int TransactionsExecuted { get; init; }
        public BigInteger MinerRewardCredited { get; init; }
        public int WithdrawalsCredited { get; init; }
        public byte[]? BlockHash { get; init; }
        public string? ErrorMessage { get; init; }
        public Exception? Exception { get; init; }
        public IReadOnlyList<TransactionExecutionResult> ExecutionResults { get; init; } = Array.Empty<TransactionExecutionResult>();

        public bool RootMatches => !this.AnyFailed()
                                   && ComputedStateRoot != null
                                   && ExpectedStateRoot != null
                                   && ByteUtil.AreEqual(ComputedStateRoot, ExpectedStateRoot);

        public IReadOnlyList<BlockValidityCheck> FailedValidityChecks => this.Failed();

        public IReadOnlyList<string> FailedChecks => this.FailedIdentifiers();

        public Nethereum.EVM.TransactionError InvalidTransactionReason { get; init; }

        public int InvalidTransactionIndex { get; init; } = -1;

        public string DescribeRejection()
        {
            if (RootMatches) return "the block passed every validity check";

            var parts = new List<string>();

            if (FailedChecks.Count > 0)
                parts.Add($"failed checks: {string.Join(", ", FailedChecks)}");

            if (ContainsInvalidTransaction)
            {
                var which = InvalidTransactionIndex >= 0
                    ? $"transaction {InvalidTransactionIndex}"
                    : "a transaction";
                parts.Add(
                    $"{which} is invalid ({InvalidTransactionReason}) — the block is invalid as authored, "
                    + "so re-executing it cannot produce a different verdict");
            }

            if (StateRootMismatch)
                parts.Add(
                    $"state root computed 0x{ComputedStateRoot?.ToHex()} != header 0x{ExpectedStateRoot?.ToHex()}");
            else if (ComputedStateRoot == null)
                parts.Add("no post-state root was produced");
            else if (ExpectedStateRoot == null)
                parts.Add("the header declares no state root to compare against");

            if (Exception != null)
                parts.Add($"{Exception.GetType().Name}: {Exception.Message}");

            return parts.Count == 0 ? "refused without naming a reason" : string.Join("; ", parts);
        }

        internal static BlockImporterResult From(
            BlockExecutionResult result, byte[]? expectedStateRoot, byte[]? blockHash) =>
            new BlockImporterResult
            {
                Fork = result.Fork,
                ComputedStateRoot = result.PostStateRoot,
                ExpectedStateRoot = expectedStateRoot,
                StateRootMismatch = result.StateRootMismatch,
                RequestsHashMismatch = result.RequestsHashMismatch,
                ReceiptsRootMismatch = result.ReceiptsRootMismatch,
                LogsBloomMismatch = result.LogsBloomMismatch,
                GasUsedMismatch = result.GasUsedMismatch,
                GasCapacityExceeded = result.GasCapacityExceeded,
                BlobGasCapacityExceeded = result.BlobGasCapacityExceeded,
                BlobGasUsedMismatch = result.BlobGasUsedMismatch,
                ExcessBlobGasMismatch = result.ExcessBlobGasMismatch,
                BlobFieldFormatMismatch = result.BlobFieldFormatMismatch,
                BaseFeeMismatch = result.BaseFeeMismatch,
                BlockAccessListHashMismatch = result.BlockAccessListHashMismatch,
                BlockAccessListGasLimitExceeded = result.BlockAccessListGasLimitExceeded,
                BlockAccessListMalformed = result.BlockAccessListMalformed,
                ContainsInvalidTransaction = result.ContainsInvalidTransaction,
                GasLimitBoundViolated = result.GasLimitBoundViolated,
                WithdrawalsRootMismatch = result.WithdrawalsRootMismatch,
                BlockAccessList = result.BlockAccessList,
                InvalidTransactionReason = result.InvalidTransactionReason,
                InvalidTransactionIndex = result.InvalidTransactionIndex,
                TransactionsExecuted = result.Receipts.Count,
                MinerRewardCredited = result.MinerRewardCredited,
                WithdrawalsCredited = result.WithdrawalsCredited,
                BlockHash = blockHash,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception,
                ExecutionResults = result.Receipts
            };
    }
}
