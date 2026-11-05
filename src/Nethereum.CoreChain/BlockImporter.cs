using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public sealed class BlockImporter : Nethereum.CoreChain.Sync.IBlockExecutor, IRewindableExecutor
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
        private readonly BlockCommit _commit;
        private readonly SideChainSupport? _sideChain;
        private readonly StatePrefetcher? _prefetcher;
        private readonly InvalidAncestry _invalidAncestry = new InvalidAncestry();

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
            : this(engine, blockStore, stateStore, transactionStore, receiptStore, logStore, uncleStore,
                   logger, nodeCommitBlockContext, atomicFlush, flushCadence, blockAccessListStore,
                   withdrawalStore, sideChain: null)
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
            IWithdrawalStore? withdrawalStore,
            SideChainSupport? sideChain)
            : this(engine, blockStore, stateStore, transactionStore, receiptStore, logStore, uncleStore,
                   logger, nodeCommitBlockContext, atomicFlush, flushCadence, blockAccessListStore,
                   withdrawalStore, sideChain, commit: null)
        {
        }

        internal BlockImporter(
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
            IWithdrawalStore? withdrawalStore,
            SideChainSupport? sideChain,
            BlockCommit? commit,
            StatePrefetcher? prefetcher = null)
        {
            _sideChain = sideChain;
            _prefetcher = prefetcher;
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
            _commit = commit ?? new BlockCommit(stateStore, nodeCommitBlockContext, atomicFlush, flushCadence, logger);
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

            var withoutExecution = await ResolveWithoutExecutionAsync(
                header, transactions, uncles, withdrawals, declaredBlockAccessList, ct).ConfigureAwait(false);
            if (withoutExecution != null) return withoutExecution;

            var executed = await ExecuteOnHeadAsync(
                header, transactions, uncles, withdrawals, declaredBlockAccessList, ct).ConfigureAwait(false);
            if (_sideChain != null && !executed.RootMatches && executed.Exception == null && !executed.UnknownParent)
            {
                var fork = _engine.Activations.ResolveAt((long)(ulong)header.BlockNumber, (ulong)header.Timestamp);
                var invalidHash = TryHash(header, fork);
                if (invalidHash != null) RememberInvalid(invalidHash, header.ParentHash, executed);
            }
            return executed;
        }

        public IBlockStore Blocks => _sideChain?.Blocks ?? _blockStore;

        public SideChainSupport? SideChain => _sideChain;

        public bool SupportsSideChains => _sideChain != null;

        public void PruneSideBlocksBelow(ulong blockNumber) => _sideChain?.SideBlocks.PruneBelow(blockNumber);

        private void RememberInvalid(byte[] blockHash, byte[]? latestValidHash, BlockImporterResult verdict)
        {
            var bodyIsTheOneTheHeaderCommitsTo = !verdict.TransactionsRootMismatch
                                                 && !verdict.UnclesHashMismatch
                                                 && !verdict.WithdrawalsRootMismatch
                                                 && !verdict.WithdrawalsRootPresenceMismatch
                                                 && !verdict.BlockAccessListHashMismatch
                                                 && !verdict.BlockAccessListMalformed
                                                 && !verdict.BlockAccessListHashPresenceMismatch;
            if (bodyIsTheOneTheHeaderCommitsTo) _invalidAncestry.Add(blockHash, latestValidHash, verdict);
        }

        public bool TryGetInvalidBlock(byte[] blockHash, out byte[]? latestValidHash)
            => _invalidAncestry.TryGet(blockHash, out latestValidHash);

        public async Task<SetCanonicalResult> SetCanonicalAsync(byte[] blockHash, CancellationToken ct = default)
        {
            if (blockHash == null) throw new ArgumentNullException(nameof(blockHash));
            var sideChain = _sideChain ?? throw new InvalidOperationException(
                "SetCanonicalAsync requires the importer to be composed with a SideChainSupport.");

            var targetHeader = await _blockStore.GetByHashAsync(blockHash).ConfigureAwait(false);
            if (targetHeader != null)
            {
                var canonicalAtNumber = await _blockStore.GetHashByNumberAsync((BigInteger)targetHeader.BlockNumber).ConfigureAwait(false);
                return canonicalAtNumber != null && ByteUtil.AreEqual(canonicalAtNumber, blockHash)
                    ? new SetCanonicalResult(SetCanonicalStatus.NoOp, null, "the block is already canonical")
                    : new SetCanonicalResult(SetCanonicalStatus.AncestryUnavailable, null, "the block is not on the canonical chain and has no side record");
            }

            var walk = await WalkSideBranchAsync(blockHash, sideChain).ConfigureAwait(false);
            if (walk.Failure != null) return walk.Failure;

            var headHeader = await _blockStore.GetLatestAsync().ConfigureAwait(false);
            var headNumber = (ulong)headHeader.BlockNumber;
            if (headNumber - walk.AncestorNumber > (ulong)sideChain.MaxReorgDepth)
                return new SetCanonicalResult(
                    SetCanonicalStatus.TooDeepReorg, null,
                    $"the reorg would drop {headNumber - walk.AncestorNumber} blocks, deeper than {sideChain.MaxReorgDepth}");

            var dropped = await ReadCanonicalBlocksAboveAsync(walk.AncestorNumber, headNumber).ConfigureAwait(false);

            if (sideChain.Metadata != null)
                sideChain.Metadata.Commit(headNumber, await _blockStore.GetHashByNumberAsync((BigInteger)headNumber).ConfigureAwait(false));

            var rewind = await RewindToAncestorAsync(sideChain.Rewind, walk.AncestorNumber, Services.RewindScope.ExecutionAndChain, ct).ConfigureAwait(false);
            if (!rewind.Rewound)
                return new SetCanonicalResult(
                    SetCanonicalStatus.RewindUnavailable, null,
                    $"{rewind.Rewind.Outcome}: {rewind.Rewind.Detail}");

            foreach (var block in dropped)
                sideChain.SideBlocks.Put(block.Hash, block.Header, block.Transactions, block.Uncles, block.Withdrawals, block.DeclaredBlockAccessList);

            var droppedHashes = new List<byte[]>(dropped.Count);
            foreach (var block in dropped) droppedHashes.Add(block.Hash);
            var droppedTransactions = new List<ISignedTransaction>();
            for (var i = dropped.Count - 1; i >= 0; i--) droppedTransactions.AddRange(dropped[i].Transactions);
            var added = new List<byte[]>(walk.Branch.Count);

            foreach (var block in walk.Branch)
            {
                var replay = await ExecuteOnHeadAsync(
                    block.Header, block.Transactions, block.Uncles, block.Withdrawals, block.DeclaredBlockAccessList, ct).ConfigureAwait(false);
                if (!replay.RootMatches)
                {
                    RememberInvalid(block.Hash, block.Header.ParentHash, replay);
                    var failed = new ChainReorganisation(
                        walk.AncestorNumber, droppedHashes, added, block.Hash, block.Header.ParentHash, replay.DescribeRejection(), droppedTransactions);
                    return await RecoverAfterFailedReplayAsync(failed, headHeader, droppedHashes, sideChain, ct).ConfigureAwait(false);
                }

                sideChain.SideBlocks.Remove(block.Hash);
                added.Add(block.Hash);
            }

            PruneSideBlocks(sideChain, (ulong)walk.Branch[walk.Branch.Count - 1].Header.BlockNumber);
            return new SetCanonicalResult(
                SetCanonicalStatus.Reorganised,
                new ChainReorganisation(walk.AncestorNumber, droppedHashes, added, null, null, null, droppedTransactions),
                null);
        }

        private Task<RewindToAncestorResult> RewindToAncestorAsync(
            Services.IRewindCoordinator rewind, ulong ancestorNumber, Services.RewindScope scope, CancellationToken ct) =>
            AncestorRewind.RunAsync(
                rewind, ancestorNumber, scope, () => _engine.StateRootCalculator.DiscardPendingState(),
                (ILogger?)_logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, ct);

        Task<RewindToAncestorResult> IRewindableExecutor.RewindToAncestorAsync(
            Services.IRewindCoordinator rewind, ulong ancestorNumber, Services.RewindScope scope, CancellationToken ct) =>
            RewindToAncestorAsync(rewind, ancestorNumber, scope, ct);

        private async Task<SetCanonicalResult> RecoverAfterFailedReplayAsync(
            ChainReorganisation failed, BlockHeader previousHead, IReadOnlyList<byte[]> droppedHashes,
            SideChainSupport sideChain, CancellationToken ct)
        {
            var invalid = new SetCanonicalResult(SetCanonicalStatus.BranchInvalid, failed, failed.Detail);
            if (sideChain.ForkChoice == null || droppedHashes.Count == 0) return invalid;

            var previousHeadHash = droppedHashes[0];
            var verdict = await sideChain.ForkChoice.ShouldAdoptAsync(previousHead, previousHeadHash, ct).ConfigureAwait(false);
            if (verdict.Outcome == Sync.ForkChoiceOutcome.AdoptIncoming)
                await SetCanonicalAsync(previousHeadHash, ct).ConfigureAwait(false);
            return invalid;
        }

        private async Task<List<SideBlock>> ReadCanonicalBlocksAboveAsync(ulong ancestorNumber, ulong headNumber)
        {
            var blocks = new List<SideBlock>();
            if (_transactionStore == null) return blocks;

            for (var number = headNumber; number > ancestorNumber; number--)
            {
                var header = await _blockStore.GetByNumberAsync(number).ConfigureAwait(false);
                var hash = await _blockStore.GetHashByNumberAsync(number).ConfigureAwait(false);
                var transactions = await _transactionStore.GetByBlockHashAsync(hash).ConfigureAwait(false)
                                   ?? new List<ISignedTransaction>();
                var uncles = _uncleStore == null ? null : await _uncleStore.GetByBlockHashAsync(hash).ConfigureAwait(false);
                var withdrawals = _withdrawalStore == null || header.WithdrawalsRoot == null
                    ? null
                    : await _withdrawalStore.GetByBlockHashAsync(hash).ConfigureAwait(false);
                var balRlp = _blockAccessListStore == null
                    ? null
                    : await _blockAccessListStore.GetByBlockHashAsync(hash).ConfigureAwait(false);
                var bal = balRlp == null ? null : BlockAccessListRLPEncoder.Current.Decode(balRlp);
                blocks.Add(new SideBlock(hash, header, transactions, uncles, withdrawals, bal));
            }

            return blocks;
        }

        private readonly struct SideBranchWalk
        {
            public SideBranchWalk(List<SideBlock> branch, ulong ancestorNumber, SetCanonicalResult? failure)
            {
                Branch = branch;
                AncestorNumber = ancestorNumber;
                Failure = failure;
            }

            public List<SideBlock> Branch { get; }
            public ulong AncestorNumber { get; }
            public SetCanonicalResult? Failure { get; }
        }

        private async Task<SideBranchWalk> WalkSideBranchAsync(byte[] tipHash, SideChainSupport sideChain)
        {
            var branch = new List<SideBlock>();
            if (!sideChain.SideBlocks.TryGet(tipHash, out var cursor))
                return Unwalkable("the block is neither canonical nor held in the side store");

            while (true)
            {
                branch.Add(cursor);
                if (_invalidAncestry.TryGet(cursor.Hash, out var latestValid) || _invalidAncestry.TryGet(cursor.Header.ParentHash, out latestValid))
                {
                    var failed = new ChainReorganisation(0, Array.Empty<byte[]>(), Array.Empty<byte[]>(), cursor.Hash, latestValid ?? cursor.Header.ParentHash, "the branch contains a block already found invalid");
                    return new SideBranchWalk(branch, 0, new SetCanonicalResult(SetCanonicalStatus.BranchInvalid, failed, failed.Detail));
                }

                if (branch.Count > sideChain.MaxReorgDepth)
                    return Unwalkable($"the side branch is longer than {sideChain.MaxReorgDepth} blocks");

                var number = (ulong)cursor.Header.BlockNumber;
                if (number == 0) return Unwalkable("the side branch reaches genesis without meeting the canonical chain");

                var canonicalParent = await _blockStore.GetHashByNumberAsync((BigInteger)(number - 1)).ConfigureAwait(false);
                if (canonicalParent != null && ByteUtil.AreEqual(canonicalParent, cursor.Header.ParentHash))
                {
                    branch.Reverse();
                    return new SideBranchWalk(branch, number - 1, null);
                }

                if (!sideChain.SideBlocks.TryGet(cursor.Header.ParentHash, out cursor))
                    return Unwalkable("an ancestor of the side branch is not held locally");
            }

            SideBranchWalk Unwalkable(string detail) =>
                new SideBranchWalk(branch, 0, new SetCanonicalResult(SetCanonicalStatus.AncestryUnavailable, null, detail));
        }

        private static void PruneSideBlocks(SideChainSupport sideChain, ulong headNumber)
        {
            if (headNumber > (ulong)sideChain.MaxReorgDepth)
                sideChain.SideBlocks.PruneBelow(headNumber - (ulong)sideChain.MaxReorgDepth);
        }

        private async Task<BlockImporterResult?> ResolveWithoutExecutionAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<Nethereum.Model.AccountChanges>? declaredBlockAccessList,
            CancellationToken ct)
        {
            var sideChain = _sideChain;
            var blockNumber = (ulong)header.BlockNumber;
            var fork = _engine.Activations.ResolveAt((long)blockNumber, (ulong)header.Timestamp);

            byte[]? blockHash = null;
            if (sideChain != null)
            {
                blockHash = TryHash(header, fork);
                if (blockHash != null)
                {
                    var canonicalAtNumber = await _blockStore.GetHashByNumberAsync((BigInteger)blockNumber).ConfigureAwait(false);
                    if (canonicalAtNumber != null && ByteUtil.AreEqual(canonicalAtNumber, blockHash))
                    {
                        return sideChain.AcknowledgeKnownCanonicalBlocks
                            ? Placed(header, fork, blockHash, BlockImportDisposition.AlreadyCanonical, computedRoot: header.StateRoot)
                            : null;
                    }

                    if (sideChain.SideBlocks.Contains(blockHash))
                        return Placed(header, fork, blockHash, BlockImportDisposition.AlreadySide, computedRoot: null);

                    if (_invalidAncestry.TryGetVerdict(blockHash, out var cachedVerdict) && cachedVerdict != null)
                        return cachedVerdict;
                }
            }

            if (header.ParentHash == null || header.ParentHash.Length == 0) return null;

            var parent = await Blocks.GetByHashAsync(header.ParentHash).ConfigureAwait(false);
            if (parent == null)
            {
                return sideChain == null
                    ? null
                    : new BlockImporterResult
                    {
                        Fork = fork,
                        ExpectedStateRoot = header.StateRoot,
                        UnknownParent = true,
                        BlockHash = blockHash,
                        Disposition = BlockImportDisposition.UnknownParent
                    };
            }

            var head = await _blockStore.GetLatestAsync().ConfigureAwait(false);
            var headHash = head == null ? null : await _blockStore.GetHashByNumberAsync((BigInteger)head.BlockNumber).ConfigureAwait(false);
            if (headHash != null && ByteUtil.AreEqual(headHash, header.ParentHash)) return null;

            blockHash ??= TryHash(header, fork);
            if (blockHash == null) return null;

            if (sideChain == null)
            {
                var canonicalAtNumber = await _blockStore.GetHashByNumberAsync((BigInteger)blockNumber).ConfigureAwait(false);
                if (canonicalAtNumber != null && ByteUtil.AreEqual(canonicalAtNumber, blockHash)) return null;

                return new BlockImporterResult
                {
                    Fork = fork,
                    ExpectedStateRoot = header.StateRoot,
                    ParentNotHead = true,
                    Disposition = BlockImportDisposition.Refused,
                    ErrorMessage = "the parent block is not the head and no side chain support is configured"
                };
            }

            return await StoreSideBlockAsync(
                header, transactions, uncles, withdrawals, declaredBlockAccessList, fork, blockHash, sideChain, ct).ConfigureAwait(false);
        }

        private static byte[]? TryHash(BlockHeader header, Nethereum.EVM.HardforkName fork)
        {
            try
            {
                return BlockHashCalculator.ForFork(header, fork);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<BlockImporterResult> StoreSideBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<Nethereum.Model.AccountChanges>? declaredBlockAccessList,
            Nethereum.EVM.HardforkName fork,
            byte[] blockHash,
            SideChainSupport sideChain,
            CancellationToken ct)
        {
            var refusal = await _engine.ValidateStructureAsync(
                header, BuildTransactionEntries(transactions), uncles, WithdrawalAdapter.Convert(withdrawals), sideChain.Blocks).ConfigureAwait(false);
            if (refusal != null)
            {
                var latestValid = CanonicalAncestorHash(header.ParentHash, sideChain);
                var refused = BlockImporterResult.From(refusal, header.StateRoot, blockHash: null, latestValidHash: latestValid);
                RememberInvalid(blockHash, latestValid, refused);
                return refused;
            }

            sideChain.SideBlocks.Put(blockHash, header, transactions, uncles, withdrawals, declaredBlockAccessList);
            var stored = Placed(header, fork, blockHash, BlockImportDisposition.StoredAsSide, computedRoot: null);

            if (sideChain.ForkChoice == null) return stored;

            var verdict = await sideChain.ForkChoice.ShouldAdoptAsync(header, blockHash, ct).ConfigureAwait(false);
            if (verdict.Outcome != Sync.ForkChoiceOutcome.AdoptIncoming) return stored;

            var adopted = await SetCanonicalAsync(blockHash, ct).ConfigureAwait(false);
            var reorganised = adopted.Status == SetCanonicalStatus.Reorganised || adopted.Status == SetCanonicalStatus.NoOp;
            return new BlockImporterResult
            {
                Fork = fork,
                ExpectedStateRoot = header.StateRoot,
                ComputedStateRoot = reorganised ? header.StateRoot : null,
                BlockHash = blockHash,
                Reorganisation = adopted.Reorganisation,
                ErrorMessage = reorganised ? null : adopted.Detail,
                LatestValidHash = adopted.Reorganisation?.FailedBlockParentHash,
                Disposition = reorganised ? BlockImportDisposition.ExtendedCanonical : BlockImportDisposition.Refused
            };
        }

        private static byte[]? CanonicalAncestorHash(byte[] hash, SideChainSupport sideChain)
        {
            var cursor = hash;
            while (cursor != null)
            {
                if (!sideChain.SideBlocks.TryGet(cursor, out var sideBlock)) return cursor;
                cursor = sideBlock.Header.ParentHash;
            }
            return null;
        }

        private static BlockImporterResult Placed(
            BlockHeader header, Nethereum.EVM.HardforkName fork, byte[] blockHash, BlockImportDisposition disposition,
            byte[]? computedRoot, byte[]? latestValidHash = null, string? errorMessage = null) =>
            new BlockImporterResult
            {
                Fork = fork,
                ExpectedStateRoot = header.StateRoot,
                ComputedStateRoot = computedRoot,
                BlockHash = blockHash,
                LatestValidHash = latestValidHash,
                ErrorMessage = errorMessage,
                Disposition = disposition
            };

        private async Task<BlockImporterResult> ExecuteOnHeadAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<Nethereum.Model.AccountChanges>? declaredBlockAccessList,
            CancellationToken ct)
        {
            var historyProvider = _stateStore as IHistoricalStateProvider;
            historyProvider?.SetCurrentBlockNumber((BigInteger)(ulong)header.BlockNumber);
            bool rootMatched = false;
            byte[]? blockHash = null;
            BlockExecutionStats? stats = null;
            var blockStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var blockWriteTicks = 0L;
            _commit.Arm((ulong)header.BlockNumber);
            try
            {
                var result = await ExecuteWithPrefetchAsync(
                    header, transactions, uncles, withdrawals, declaredBlockAccessList, ct).ConfigureAwait(false);

                if (ExecutionEarnedNoPersistence(result))
                {
                    return BlockImporterResult.From(result, header.StateRoot, blockHash: null);
                }

                stats = result.Stats;
                blockHash = BlockHashCalculator.ForFork(header, result.Fork);
                var blockWriteStart = System.Diagnostics.Stopwatch.GetTimestamp();
                await PersistBlockAndArtifactsAsync(header, result, uncles, withdrawals, transactions, blockHash).ConfigureAwait(false);
                blockWriteTicks = System.Diagnostics.Stopwatch.GetTimestamp() - blockWriteStart;

                rootMatched = true;
                return BlockImporterResult.From(result, header.StateRoot, blockHash);
            }
            finally
            {
                _commit.Clear();
                if (rootMatched)
                {
                    var flushStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    await _commit.FlushAndJournalMatchedBlockAsync(historyProvider, header, blockHash!).ConfigureAwait(false);
                    var flushTicks = System.Diagnostics.Stopwatch.GetTimestamp() - flushStart;
                    CommitCanonicalHead(header, blockHash!);
                    ReportExecutionStats(stats, header, blockStart, blockWriteTicks, flushTicks);
                }
                else
                {
                    if (ct.IsCancellationRequested)
                        await _commit.DiscardCancelledBlockAsync(historyProvider).ConfigureAwait(false);
                    else
                        await _commit.DiscardAndRevertUnpersistedBlockAsync(historyProvider).ConfigureAwait(false);
                }
            }
        }

        private async Task<BlockExecutionResult> ExecuteWithPrefetchAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<Nethereum.Model.AccountChanges>? declaredBlockAccessList,
            CancellationToken ct)
        {
            var prefetch = _prefetcher?.Begin(header, transactions);
            try
            {
                return await _engine.ExecuteAsync(
                    header,
                    BuildTransactionEntries(transactions),
                    uncles,
                    WithdrawalAdapter.Convert(withdrawals),
                    new BlockExecutionOptions
                    {
                        Role = BlockExecutionRole.Validating,
                        DeclaredBlockAccessList = declaredBlockAccessList,
                        Prefetch = prefetch
                    },
                    ct).ConfigureAwait(false);
            }
            finally
            {
                if (_prefetcher != null) ReportPrefetch(header, transactions.Count, prefetch);
            }
        }

        private void ReportPrefetch(BlockHeader header, int transactionCount, PrefetchSession? prefetch)
        {
            var planned = prefetch != null;
            var outcome = prefetch?.Stop() ?? new PrefetchOutcome(0, transactionCount, 0);
            var triePaths = prefetch?.TriePathOutcome ?? default;
            _logger?.LogInformation(
                "block.prefetch.stats block={BlockNumber} txs={TxCount} prefetched={Prefetched} skipped={Skipped} failed={Failed} triePathsEnqueued={TriePathsEnqueued} triePathsWarmed={TriePathsWarmed} triePathsDropped={TriePathsDropped} triePathsFailed={TriePathsFailed} triePathsSkipped={TriePathsSkipped} planned={Planned}",
                header.BlockNumber, transactionCount, outcome.Prefetched, outcome.Skipped, outcome.Failed,
                triePaths.Enqueued, triePaths.Warmed, triePaths.Dropped, triePaths.Failed, triePaths.Skipped, planned);
        }

        private void ReportExecutionStats(
            BlockExecutionStats? stats, BlockHeader header, long blockStart, long blockWriteTicks, long flushTicks)
        {
            if (stats == null) return;
            stats.BlockWrite = TicksToTimeSpan(blockWriteTicks);
            stats.DatabaseCommit += TicksToTimeSpan(flushTicks);
            stats.SetTotal(TicksToTimeSpan(System.Diagnostics.Stopwatch.GetTimestamp() - blockStart));
            _logger?.LogInformation(
                "block.exec.stats block={BlockNumber} txs={TxCount} gas={Gas} mgasps={MgasPerSecond:F3} total_ms={TotalMs:F3} execution_ms={ExecutionMs:F3} validation_ms={ValidationMs:F3} account_reads_ms={AccountReadsMs:F3} storage_reads_ms={StorageReadsMs:F3} code_reads_ms={CodeReadsMs:F3} account_updates_ms={AccountUpdatesMs:F3} account_hashes_ms={AccountHashesMs:F3} account_commits_ms={AccountCommitsMs:F3} storage_updates_ms={StorageUpdatesMs:F3} storage_commits_ms={StorageCommitsMs:F3} db_commit_ms={DbCommitMs:F3} block_write_ms={BlockWriteMs:F3} accounts_loaded={AccountsLoaded} accounts_updated={AccountsUpdated} accounts_deleted={AccountsDeleted} storage_loaded={StorageLoaded} storage_updated={StorageUpdated} storage_deleted={StorageDeleted} code_loaded={CodeLoaded} code_load_bytes={CodeLoadBytes} code_updated={CodeUpdated} code_update_bytes={CodeUpdateBytes} precompiles={Precompiles}",
                header.BlockNumber, stats.TransactionCount, stats.GasUsed, stats.MgasPerSecond,
                stats.TotalTime.TotalMilliseconds, stats.Execution.TotalMilliseconds, stats.Validation.TotalMilliseconds,
                stats.AccountReads.TotalMilliseconds, stats.StorageReads.TotalMilliseconds, stats.CodeReads.TotalMilliseconds,
                stats.AccountUpdates.TotalMilliseconds, stats.AccountHashes.TotalMilliseconds, stats.AccountCommits.TotalMilliseconds,
                stats.StorageUpdates.TotalMilliseconds, stats.StorageCommits.TotalMilliseconds,
                stats.DatabaseCommit.TotalMilliseconds, stats.BlockWrite.TotalMilliseconds,
                stats.AccountLoaded, stats.AccountUpdated, stats.AccountDeleted,
                stats.StorageLoaded, stats.StorageUpdated, stats.StorageDeleted,
                stats.CodeLoaded, stats.CodeLoadBytes, stats.CodeUpdated, stats.CodeUpdateBytes,
                stats.PrecompileCalls.Count == 0 ? "-" : stats.PrecompileCallsKey);
        }

        private static TimeSpan TicksToTimeSpan(long ticks)
            => TimeSpan.FromTicks((long)(ticks * ((double)TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency)));

        private void CommitCanonicalHead(BlockHeader header, byte[] blockHash)
        {
            if (_sideChain == null) return;
            _sideChain.Metadata?.Commit((ulong)header.BlockNumber, blockHash);
            PruneSideBlocks(_sideChain, (ulong)header.BlockNumber);
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
        public BlockExecutionStats? Stats { get; init; }
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

        public bool BlockRlpSizeExceeded { get; init; }

        public bool UnknownParent { get; init; }

        public bool ParentNotHead { get; init; }

        private BlockImportDisposition? _disposition;

        public BlockImportDisposition Disposition
        {
            get => _disposition ?? (UnknownParent ? BlockImportDisposition.UnknownParent : RootMatches ? BlockImportDisposition.ExtendedCanonical : BlockImportDisposition.Refused);
            init => _disposition = value;
        }

        public byte[]? LatestValidHash { get; init; }

        public ChainReorganisation? Reorganisation { get; init; }

        public bool BlockNumberMismatch { get; init; }

        public bool TimestampNotAfterParent { get; init; }

        public bool FutureBlock { get; init; }

        public bool ExtraDataTooLong { get; init; }

        public bool DaoExtraDataMismatch { get; init; }

        public bool GasLimitTooBig { get; init; }

        public bool DifficultyMismatch { get; init; }

        public bool NonceInvalid { get; init; }

        public bool UnclesHashMismatch { get; init; }

        public bool UnclesInvalid { get; init; }

        public bool TransactionsRootMismatch { get; init; }

        public bool WithdrawalsRootPresenceMismatch { get; init; }

        public bool ParentBeaconRootPresenceMismatch { get; init; }

        public bool BlockAccessListHashPresenceMismatch { get; init; }

        public bool SlotNumberPresenceMismatch { get; init; }

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
            BlockExecutionResult result, byte[]? expectedStateRoot, byte[]? blockHash, byte[]? latestValidHash = null) =>
            new BlockImporterResult
            {
                Fork = result.Fork,
                ComputedStateRoot = result.PostStateRoot,
                ExpectedStateRoot = expectedStateRoot,
                Stats = result.Stats,
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
                BlockRlpSizeExceeded = result.BlockRlpSizeExceeded,
                UnknownParent = result.UnknownParent,
                BlockNumberMismatch = result.BlockNumberMismatch,
                TimestampNotAfterParent = result.TimestampNotAfterParent,
                FutureBlock = result.FutureBlock,
                ExtraDataTooLong = result.ExtraDataTooLong,
                DaoExtraDataMismatch = result.DaoExtraDataMismatch,
                GasLimitTooBig = result.GasLimitTooBig,
                DifficultyMismatch = result.DifficultyMismatch,
                NonceInvalid = result.NonceInvalid,
                UnclesHashMismatch = result.UnclesHashMismatch,
                UnclesInvalid = result.UnclesInvalid,
                TransactionsRootMismatch = result.TransactionsRootMismatch,
                WithdrawalsRootPresenceMismatch = result.WithdrawalsRootPresenceMismatch,
                ParentBeaconRootPresenceMismatch = result.ParentBeaconRootPresenceMismatch,
                BlockAccessListHashPresenceMismatch = result.BlockAccessListHashPresenceMismatch,
                SlotNumberPresenceMismatch = result.SlotNumberPresenceMismatch,
                ParentNotHead = result.ParentNotHead,
                BlockAccessList = result.BlockAccessList,
                InvalidTransactionReason = result.InvalidTransactionReason,
                InvalidTransactionIndex = result.InvalidTransactionIndex,
                TransactionsExecuted = result.Receipts.Count,
                MinerRewardCredited = result.MinerRewardCredited,
                WithdrawalsCredited = result.WithdrawalsCredited,
                BlockHash = blockHash,
                LatestValidHash = latestValidHash,
                ErrorMessage = result.ErrorMessage,
                Exception = result.Exception,
                ExecutionResults = result.Receipts
            };
    }
}
