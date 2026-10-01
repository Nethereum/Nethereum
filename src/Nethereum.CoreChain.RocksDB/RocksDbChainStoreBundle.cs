using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed partial class RocksDbChainStoreBundle : IChainStoreBundle, IBatchedBlockPersister, IBackpressureExemptPersister, IBulkDurabilityBoundary, IHealNodeSinkProvider, IHistoryWriteBackpressure, IBackfillPauseControl, IStateWriteBackpressure, IStateCompaction, IFlatStateReconciler, IFlatStateTrieGenerator, IBulkFlatStateSinkProvider, INodeHistoryRecoverable, Services.IHistoricalProofServingBundle, Services.ILatestProofServingBundle, IAtomicBlockFlush, IPromotionFloorGuard
    {

        private const int NodeHistoryPruneIntervalBlocks = 128;

        private const int BulkCheckpointIntervalBlocks = 65_536;
        private const int BulkCheckpointIntervalSecondsDisabled = 0;
        private const int BulkCheckpointMaxPendingEntries = 6_000_000;

        private const int FreezerIndexCheckpointIntervalBlocks = 4_096;
        private const int FreezerIndexCheckpointMaxPendingEntries = 1_000_000;

        private const int FreezerDecodedClusterCacheBlocks = 256;

        public IStateStore         State        { get; }
        public ITrieNodeStore      TrieNodes    { get; }
        public ITrieNodeStore          StateTrieNodes { get; }
        public NodeCommitBlockContext NodeCommitBlockSource { get; }
        public IBlockStore         Blocks       { get; }
        public ITransactionStore   Transactions { get; }
        public IUncleStore         Uncles       { get; }
        public IWithdrawalStore    Withdrawals  { get; }
        public IBlockAccessListStore BlockAccessLists { get; }
        public IReceiptStore       Receipts     { get; }
        public ILogStore           Logs         { get; }
        public IChainMetadataStore Metadata     { get; }
        public IStateDiffStore     Diffs        { get; }
        public bool                JournalEnabled { get; }
        public string              DataDir      { get; }

        public Stores.HistoricalNodeServing NodeServing { get; }

        Services.IHistoricalProofCapable Services.IHistoricalProofServingBundle.NodeServing => NodeServing;

        public ITrieNodeStore LatestProofNodeStore { get; }

        public RocksDbManager Rocks => _rocks;

        private readonly RocksDbManager _rocks;
        private readonly RocksDbManager _historyRocks;
        private readonly RocksDbManager _freezerHistoryRocks;
        private readonly bool _ownsManager;
        private readonly Stores.RocksDbLogStore _logIndexStore;
        private readonly Stores.RocksDbBlockStore _historyBlockStore;
        private readonly Stores.RocksDbTransactionStore _historyTransactionStore;
        private readonly Stores.RocksDbReceiptStore _historyReceiptStore;
        private readonly Stores.RocksDbWithdrawalStore _hotWithdrawalStore;
        private readonly Stores.RocksDbHotBlockWindowStore _hotWindow;
        private readonly Stores.RocksDbPromotionService _promotionService;
        private readonly ulong _promotionMaxHistoryBlocks;
        private readonly History.SyncBulkSaveService _bulkSave;
        private readonly Nethereum.Freezer.Freezer _freezerAppend;
        private readonly Freezer.FreezerBackgroundIndexer _freezerIndexer;
        private readonly Freezer.FreezerAppendService _freezerAppendService;
        private readonly Freezer.FreezerPromotionDriver _freezerPromotionDriver;
        private readonly object _freezerAppendLock;

        private readonly Nethereum.Model.IBlockEncodingProvider _provider = Nethereum.Model.RlpBlockEncodingProvider.Instance;
        private readonly Serialization.RocksDbSerializer _serializer = Serialization.RocksDbSerializer.Default;
        private static readonly System.Threading.Tasks.ParallelOptions _persistEncodeOptions =
            new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = System.Math.Max(1, System.Environment.ProcessorCount - 2) };

        private readonly Stores.RocksDbPathTrieNodeStore _healPathStore;
        private readonly RocksDbStateStore _rawFlatState;
        private readonly Stores.RocksDbNodeReverseDiffStore _nodeReverseDiffStore;
        private readonly Stores.INodeHistoryFloorPolicy _nodeHistoryFloor;
        private readonly Stores.WindowDirtyLayers _windowLayers;
        public Stores.WindowDirtyLayers WindowLayers => _windowLayers;
        private readonly Stores.PendingFlushFlatOverlay _pendingFlushFlatOverlay;
        internal Stores.PendingFlushFlatOverlay PendingFlushFlatOverlay => _pendingFlushFlatOverlay;
        public RocksDbManager FreezerHistoryRocksForTests => _freezerHistoryRocks;
        private long _lastPrunedQuantum;
        private Stores.RocksDbWritePressureMonitor _pressureMonitor;
        private readonly Stores.RocksDbCheckpointManager _checkpointManager;
        private readonly Stores.RocksDbRecoveryService _recoveryService;

        private (ulong Block, IList<Nethereum.Model.Withdrawal> Withdrawals)? _armedWithdrawals;

        private ulong? _stagedFlushOwnsBlock;
        private bool _stagedFlushOwnsWithdrawals;

        private Task _pendingFlush = Task.CompletedTask;

        private RocksDbChainStoreBundle(
            IStateStore state, ITrieNodeStore trie, ITrieNodeStore stateTrieNodes,
            NodeCommitBlockContext nodeCommitBlockSource, IBlockStore blocks,
            ITransactionStore transactions, IUncleStore uncles,
            IWithdrawalStore withdrawals,
            IBlockAccessListStore blockAccessLists,
            IReceiptStore receipts, ILogStore logs,
            IChainMetadataStore metadata, IStateDiffStore diffs,
            bool journalEnabled, string dataDir, RocksDbManager rocks, bool ownsManager,
            History.SyncBulkSaveService bulkSave = null,
            Stores.HistoricalNodeServing nodeServing = null,
            ITrieNodeStore latestProofNodeStore = null,
            Stores.RocksDbPathTrieNodeStore healPathStore = null,
            RocksDbStateStore rawFlatState = null,
            Stores.RocksDbNodeReverseDiffStore nodeReverseDiffStore = null,
            Stores.INodeHistoryFloorPolicy nodeHistoryFloor = null,
            Stores.WindowDirtyLayers windowLayers = null,
            Stores.PendingFlushFlatOverlay pendingFlushFlatOverlay = null,
            RocksDbManager historyRocks = null,
            string historyDataDir = null,
            Stores.RocksDbLogStore logIndexStore = null,
            Stores.RocksDbBlockStore historyBlockStore = null,
            Stores.RocksDbTransactionStore historyTransactionStore = null,
            Stores.RocksDbReceiptStore historyReceiptStore = null,
            Stores.RocksDbWithdrawalStore hotWithdrawalStore = null,
            Stores.RocksDbHotBlockWindowStore hotWindow = null,
            Stores.RocksDbPromotionService promotionService = null,
            ulong promotionMaxHistoryBlocks = 0,
            Nethereum.Freezer.Freezer freezerAppend = null,
            Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet freezerCodecs = null,
            Stores.RocksDbFilterMapsStore fmStore = null,
            Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer fmIndexer = null,
            RocksDbManager freezerHistoryRocks = null,
            string freezerHistoryDataDir = null,
            Nethereum.CoreChain.Freezer.FreezerPromotionService freezerPromotionService = null,
            Stores.RocksDbHotBlockWindowSourceAdapter freezerHotWindowAdapter = null,
            History.BulkIndexIngestor freezerIndexIngestor = null,
            Freezer.IFreezerIndexProgress freezerIndexProgress = null,
            object freezerAppendLock = null)
        {
            _freezerAppendLock = freezerAppendLock ?? new object();
            _freezerAppend = freezerAppend;
            _freezerHistoryRocks = freezerHistoryRocks;
            _historyBlockStore = historyBlockStore;
            _historyTransactionStore = historyTransactionStore;
            _historyReceiptStore = historyReceiptStore;
            _hotWithdrawalStore = hotWithdrawalStore;
            _hotWindow = hotWindow;
            _promotionService = promotionService;
            _promotionMaxHistoryBlocks = promotionMaxHistoryBlocks;
            _healPathStore = healPathStore;
            _rawFlatState = rawFlatState;
            _nodeReverseDiffStore = nodeReverseDiffStore;
            _nodeHistoryFloor = nodeHistoryFloor;
            _logIndexStore = logIndexStore;
            _windowLayers = windowLayers;
            _pendingFlushFlatOverlay = pendingFlushFlatOverlay;
            _bulkSave = bulkSave;
            NodeServing = nodeServing;
            LatestProofNodeStore = latestProofNodeStore;
            State = state;
            TrieNodes = trie;
            StateTrieNodes = stateTrieNodes;
            NodeCommitBlockSource = nodeCommitBlockSource;
            Blocks = blocks;
            Transactions = transactions;
            Uncles = uncles;
            Withdrawals = withdrawals;
            BlockAccessLists = blockAccessLists;
            Receipts = receipts;
            Logs = logs;
            Metadata = metadata;
            Diffs = diffs;
            JournalEnabled = journalEnabled;
            DataDir = dataDir;
            _rocks = rocks;
            _historyRocks = historyRocks ?? rocks;
            var isSplit = !ReferenceEquals(_historyRocks, _rocks);
            var pairingGuard = isSplit ? new Stores.StorePairingGuard(dataDir, historyDataDir) : null;
            _pressureMonitor = new Stores.RocksDbWritePressureMonitor(rocks, dataDir, _historyRocks, freezerHistoryRocks);
            _freezerIndexer = freezerAppend == null
                ? null
                : new Freezer.FreezerBackgroundIndexer(
                    freezerAppend, _pressureMonitor, freezerCodecs, freezerIndexIngestor, fmStore, fmIndexer,
                    freezerIndexProgress, rocks.Options.BackgroundFreezeIndexing,
                    rocks.Options.FreezerBackgroundDegreeOfParallelism);
            _freezerAppendService = freezerAppend == null
                ? null
                : new Freezer.FreezerAppendService(freezerAppend, freezerCodecs, rocks, _freezerIndexer, _freezerAppendLock);
            _freezerPromotionDriver = freezerPromotionService == null
                ? null
                : new Freezer.FreezerPromotionDriver(_freezerAppendService, _freezerIndexer, freezerPromotionService, freezerHotWindowAdapter);
            var isFreezerCheckpoint = _freezerHistoryRocks != null;
            _checkpointManager = (isFreezerCheckpoint, isSplit) switch
            {
                (true, true) => new Stores.RocksDbCheckpointManager(
                    rocks, dataDir, metadata,
                    _historyRocks, historyDataDir,
                    _freezerHistoryRocks, freezerHistoryDataDir,
                    rocks.Options.FreezerHistoryDirectory, _freezerAppendLock, () => _freezerAppend.Items),
                (true, false) => new Stores.RocksDbCheckpointManager(
                    rocks, dataDir, metadata,
                    _freezerHistoryRocks, freezerHistoryDataDir,
                    rocks.Options.FreezerHistoryDirectory, _freezerAppendLock, () => _freezerAppend.Items),
                (false, true) => new Stores.RocksDbCheckpointManager(rocks, dataDir, metadata, _historyRocks, historyDataDir),
                _ => new Stores.RocksDbCheckpointManager(rocks, dataDir, metadata),
            };
            _recoveryService = new Stores.RocksDbRecoveryService(rocks, rawFlatState, stateTrieNodes, blocks, metadata, diffs, journalEnabled, pairingGuard, _promotionService);
            _ownsManager = ownsManager;
        }

        public const string CoreSubDir = "core";
        public const string HistorySubDir = "history";

        public const string FreezerHistorySubDir = "freezer-history";

        public enum StorageLayout { Fresh, ExistingSingle, ExistingSplit }

        public static StorageLayout DetectStorageLayout(string dataDir)
        {
            if (HasRealRocksDbAt(dataDir)) return StorageLayout.ExistingSingle;
            if (HasRealRocksDbAt(Path.Combine(dataDir, CoreSubDir))) return StorageLayout.ExistingSplit;
            return StorageLayout.Fresh;
        }

        private static bool HasRealRocksDbAt(string dir)
            => File.Exists(Path.Combine(dir, "IDENTITY")) || File.Exists(Path.Combine(dir, "CURRENT"));

        public static bool ResolveEffectiveSplit(StorageLayout layout, bool requestedSplit) => layout switch
        {
            StorageLayout.ExistingSingle => false,
            StorageLayout.ExistingSplit => true,
            _ => requestedSplit,
        };

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "chain-store-bundle", "RocksDbChainStoreBundle.Open — open the RocksDB chain-store bundle")]
        public static RocksDbChainStoreBundle Open(
            string dataDir, HistoricalStateOptions journalOptions = null, bool bulkSync = false,
            RocksDbStorageOptions storageOptions = null)
            => Open(dataDir, journalOptions, bulkSync, storageOptions, signer: null);

        public static RocksDbChainStoreBundle Open(
            string dataDir, HistoricalStateOptions journalOptions, bool bulkSync,
            RocksDbStorageOptions storageOptions,
            Nethereum.Model.ITransactionVerificationAndRecovery signer)
        {
            if (string.IsNullOrEmpty(dataDir)) throw new ArgumentException("Data dir required", nameof(dataDir));
            var options = storageOptions ?? new RocksDbStorageOptions();

            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(dataDir);
            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(Path.Combine(dataDir, CoreSubDir));
            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(Path.Combine(dataDir, HistorySubDir));
            if (options.UseFreezerHistory)
            {
                Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(Path.Combine(dataDir, FreezerHistorySubDir));
                Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(Path.Combine(dataDir, CoreSubDir, FreezerHistorySubDir));
            }

            var layout = DetectStorageLayout(dataDir);
            var useSplit = ResolveEffectiveSplit(layout, options.SplitHistoryStore);
            if (useSplit != options.SplitHistoryStore)
            {
                var message = layout == StorageLayout.ExistingSingle
                    ? $"[RocksDbChainStoreBundle] existing single-DB at {dataDir}; ignoring SplitHistoryStore=true to avoid orphaning it — re-sync into a fresh dir to use split."
                    : $"[RocksDbChainStoreBundle] existing split-DB at {dataDir}; forcing SplitHistoryStore on to avoid orphaning core/history.";
                Console.Error.WriteLine(message);
            }

            if (useSplit)
            {
                var coreDir = Path.Combine(dataDir, CoreSubDir);
                var historyDir = Path.Combine(dataDir, HistorySubDir);
                Directory.CreateDirectory(coreDir);
                Directory.CreateDirectory(historyDir);

                var coreOptions = options.Clone();
                coreOptions.DatabasePath = coreDir;
                var historyOptions = options.Clone();
                historyOptions.DatabasePath = historyDir;

                var rocksCore = new RocksDbManager(coreOptions, CatalogueScope.Core);
                var rocksHistory = new RocksDbManager(historyOptions, CatalogueScope.History);
                return FromManager(rocksCore, coreDir, journalOptions, ownsManager: true, bulkSync: bulkSync,
                    flatStateCache: null, historyRocks: rocksHistory, historyDataDir: historyDir, signer: signer);
            }

            Directory.CreateDirectory(dataDir);
            options.DatabasePath = dataDir;
            var rocks = new RocksDbManager(options);
            return FromManager(rocks, dataDir, journalOptions, ownsManager: true, bulkSync: bulkSync,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: signer);
        }

        public static RocksDbChainStoreBundle FromManager(
            RocksDbManager rocks, string dataDir,
            HistoricalStateOptions journalOptions = null, bool ownsManager = true, bool bulkSync = false,
            Storage.FlatStateCache flatStateCache = null,
            RocksDbManager historyRocks = null, string historyDataDir = null)
            => FromManager(rocks, dataDir, journalOptions, ownsManager, bulkSync, flatStateCache,
                historyRocks, historyDataDir, signer: null);

        public static RocksDbChainStoreBundle FromManager(
            RocksDbManager rocks, string dataDir,
            HistoricalStateOptions journalOptions, bool ownsManager, bool bulkSync,
            Storage.FlatStateCache flatStateCache,
            RocksDbManager historyRocks, string historyDataDir,
            Nethereum.Model.ITransactionVerificationAndRecovery signer)
        {
            if (rocks == null) throw new ArgumentNullException(nameof(rocks));
            if (string.IsNullOrEmpty(dataDir)) throw new ArgumentException("Data dir required", nameof(dataDir));

            var historyManager = historyRocks ?? rocks;
            var isSplit = !ReferenceEquals(historyManager, rocks);
            var promotionActive = rocks.Options.PromotionEnabled;
            if (isSplit && string.IsNullOrEmpty(historyDataDir))
                throw new ArgumentException("historyDataDir required when historyRocks is supplied", nameof(historyDataDir));
            if (isSplit && promotionActive)
                throw new NotSupportedException(
                    "Promotion mode requires a single physical database and cannot be combined with " +
                    "SplitHistoryStore's separate history database. Use one or the other.");
            if (isSplit && bulkSync && rocks.Options.EnableLogIndex)
                throw new NotSupportedException(
                    "SplitHistoryStore + bulkSync (the from-genesis WAL-off firehose) + EnableLogIndex is not " +
                    "supported in this slice: SyncBulkSaveService's block-bloom/log-index extras are core-scope " +
                    "CFs it cannot reach on a history-only manager. Use the ordinary (non-bulkSync) backfill path " +
                    "with SplitHistoryStore, or bulkSync + SplitHistoryStore with EnableLogIndex off (the default), " +
                    "or bulkSync without SplitHistoryStore.");

            if (promotionActive && rocks.Options.EnableLogIndex)
                throw new NotSupportedException(
                    "Promotion mode does not support the opt-in log index (EnableLogIndex): its reorg-able " +
                    "log-index rows are not managed by promotion. Disable one of them.");

            if (isSplit && rocks.Options.UseFreezerHistory)
                throw new NotSupportedException(
                    "SplitHistoryStore and UseFreezerHistory cannot be combined: the freezer IS the history " +
                    "backend (its own narrow freezer-history database carries the by-hash/log index), so a " +
                    "second full history database from SplitHistoryStore would sit alongside it unused. Use one " +
                    "or the other.");
            if (rocks.Options.UseFreezerHistory && string.IsNullOrWhiteSpace(rocks.Options.FreezerHistoryDirectory))
                throw new ArgumentException(
                    "UseFreezerHistory requires FreezerHistoryDirectory (the append-only archive's directory).",
                    nameof(rocks));
            if (rocks.Options.UseFreezerHistory && signer == null)
                throw new ArgumentException(
                    "UseFreezerHistory requires a signer (ITransactionVerificationAndRecovery) to recover senders " +
                    "for frozen blocks' transactions and receipts.",
                    nameof(signer));

            rocks.Options.Validate();

            if (rocks.Options.TrieNodeHistoryBlocks > 0
                && journalOptions != null && journalOptions.MaxHistoryBlocks > 0
                && rocks.Options.TrieNodeHistoryBlocks > journalOptions.MaxHistoryBlocks)
            {
                throw new InvalidOperationException(
                    $"TrieNodeHistoryBlocks={rocks.Options.TrieNodeHistoryBlocks} exceeds the value-history window " +
                    $"MaxHistoryBlocks={journalOptions.MaxHistoryBlocks}: a rewind cannot reconstruct node state beyond " +
                    "the value journal it replays against. Set TrieNodeHistoryBlocks <= MaxHistoryBlocks (or 0 for full history on both).");
            }

            var promotionMaxHistoryBlocks = journalOptions != null && journalOptions.MaxHistoryBlocks > 0
                ? (ulong)journalOptions.MaxHistoryBlocks
                : 0UL;
            if (promotionActive && promotionMaxHistoryBlocks > int.MaxValue)
                throw new InvalidOperationException(
                    $"MaxHistoryBlocks={promotionMaxHistoryBlocks} exceeds the hot window's representable size " +
                    $"({int.MaxValue:N0}); promotion mode cannot derive a matching hot window.");
            var hotWindowBlocks = promotionActive && promotionMaxHistoryBlocks > 0
                ? (int)promotionMaxHistoryBlocks
                : rocks.Options.HotWindowBlocks;

            var historyBlockStore = new RocksDbBlockStore(historyManager);
            var historyTransactionStore = new RocksDbTransactionStore(historyManager, historyBlockStore);

            var hotWindowActive = isSplit || promotionActive;
            var hotWindow = hotWindowActive
                ? new Stores.RocksDbHotBlockWindowStore(rocks, hotWindowBlocks, evictOnWrite: isSplit)
                : null;
            var writeThroughHistory = !promotionActive;
            IBlockStore blocks = hotWindowActive
                ? (IBlockStore)new Stores.CompositeBlockStore(historyBlockStore, hotWindow, writeThroughHistory)
                : historyBlockStore;
            ITransactionStore transactions = hotWindowActive
                ? (ITransactionStore)new Stores.CompositeTransactionStore(historyTransactionStore, hotWindow, writeThroughHistory)
                : historyTransactionStore;
            var diffStore = new RocksDbStateDiffStore(rocks);
            var rawState = new RocksDbStateStore(rocks);

            var windowLayers = (rocks.Options.PathKeyedState && rocks.Options.TrieNodeHistoryBlocks >= 0)
                ? new Stores.WindowDirtyLayers()
                : null;
            var pendingFlushFlatOverlay = new Stores.PendingFlushFlatOverlay();

            IStateStore wired = new StateLayer().Stores.MainnetFollower(rawState, diffStore, journalOptions, flatStateCache, pendingFlushFlatOverlay);

            History.SyncBulkSaveService bulk = null;
            if (bulkSync)
            {
                bulk = new History.SyncBulkSaveService(
                    (isSplit ? historyManager : rocks).Database,
                    Path.Combine(isSplit ? historyDataDir : dataDir, "bulk-scratch"),
                    checkpointIntervalBlocks: BulkCheckpointIntervalBlocks,
                    checkpointIntervalSeconds: BulkCheckpointIntervalSecondsDisabled,
                    maxPendingEntries: BulkCheckpointMaxPendingEntries);
                bulk.PrepareResume();
            }

            var trieNodeStore = new RocksDbTrieNodeStore(rocks);
            var metadataStore = new RocksDbChainMetadataStore(rocks);

            if (bulk != null)
            {
                var bulkCheckpoint = bulk.LastCompletedBlock();
                if (bulkCheckpoint.HasValue)
                {
                    var lastBlock = metadataStore.GetLastBlock();
                    var bodyCursor = metadataStore.GetLastFetchedBody();
                    var headerCursor = metadataStore.GetLastFetchedHeader();
                    bool cursorInFillRegion = lastBlock == 0 || bodyCursor < lastBlock;
                    if (cursorInFillRegion && bodyCursor > bulkCheckpoint.Value)
                        metadataStore.SetLastFetchedBody(bulkCheckpoint.Value);
                    if (cursorInFillRegion && headerCursor == bodyCursor && headerCursor > bulkCheckpoint.Value)
                        metadataStore.SetLastFetchedHeader(bulkCheckpoint.Value);
                }
            }
            ITrieNodeStore stateTrieNodes;
            NodeCommitBlockContext nodeCommitBlockSource = null;
            Stores.HistoricalNodeServing nodeServing = null;
            ITrieNodeStore latestProofNodeStore = null;
            Stores.RocksDbPathTrieNodeStore healPathStore = null;
            Stores.RocksDbNodeReverseDiffStore nodeReverseDiffStore = null;
            Stores.INodeHistoryFloorPolicy nodeHistoryFloor = null;
            if (rocks.Options.PathKeyedState)
            {
                var pathStore = new RocksDbPathTrieNodeStore(rocks);
                healPathStore = pathStore;
                latestProofNodeStore = new Stores.PathKeyedProofNodeStore(pathStore);
                if (rocks.Options.TrieNodeHistoryBlocks >= 0)
                {
                    nodeCommitBlockSource = new NodeCommitBlockContext();
                    var journal = new Stores.RocksDbNodeReverseDiffStore(
                        rocks, buildKeyMajorIndex: rocks.Options.TrieNodeHistoryIndex);
                    nodeReverseDiffStore = journal;
                    var floor = new Stores.FixedWindowFloorPolicy(rocks.Options.TrieNodeHistoryBlocks);
                    nodeHistoryFloor = floor;
                    var journalingPathStore = new Stores.JournalingPathNodeStore(
                        pathStore, journal, nodeCommitBlockSource, floor, NodeHistoryPruneIntervalBlocks);
                    stateTrieNodes = new Stores.CapturingJournalingPathNodeStore(journalingPathStore, nodeCommitBlockSource, windowLayers);

                    if (rocks.Options.TrieNodeHistoryIndex)
                    {
                        nodeServing = new Stores.HistoricalNodeServing(
                            journal, pathStore, floor, indexOn: true, state: wired, blocks: blocks, metadata: metadataStore);
                    }
                }
                else
                {
                    stateTrieNodes = pathStore;
                }
            }
            else
            {
                stateTrieNodes = trieNodeStore;
            }

            var l1LogStore = new Stores.HistoryBloomScanLogStore(historyManager,
                promotionCursor: promotionActive ? metadataStore : null);
            var logIndexStore = rocks.Options.EnableLogIndex ? new RocksDbLogStore(rocks) : null;
            ILogStore logs = new Stores.CompositeLogStore(l1LogStore, logIndexStore);

            var historyUncleStore = new RocksDbUncleStore(historyManager, blocks);
            var historyWithdrawalStore = new RocksDbWithdrawalStore(historyManager, blocks);
            var historyReceiptStore = new RocksDbReceiptStore(historyManager, blocks);
            var historyBlockAccessListStore = new Stores.RocksDbBlockAccessListStore(historyManager);

            Stores.RocksDbWithdrawalStore hotWithdrawalStore = null;
            IUncleStore uncles;
            IWithdrawalStore withdrawals;
            IReceiptStore receipts;
            IBlockAccessListStore blockAccessLists;
            if (promotionActive)
            {
                var hotUncleStore = new RocksDbUncleStore(rocks, blocks, blockMetaCf: RocksDbManager.CF_HOT_BLOCK_META, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
                hotWithdrawalStore = new RocksDbWithdrawalStore(rocks, blocks, blockMetaCf: RocksDbManager.CF_HOT_BLOCK_META, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
                var hotReceiptStore = new RocksDbReceiptStore(rocks, blocks, receiptBodyCf: RocksDbManager.CF_HOT_RECEIPT_BODY, txHashIndexCf: RocksDbManager.CF_HOT_TX_HASH_INDEX, writeTxHashIndex: false);
                var hotBlockAccessListStore = new Stores.RocksDbBlockAccessListStore(rocks,
                    blockAccessListCf: RocksDbManager.CF_HOT_BLOCK_ACCESS_LIST, blockHashIndexCf: RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
                uncles = new Stores.CompositeUncleStore(historyUncleStore, hotUncleStore, hotWindow);
                withdrawals = new Stores.CompositeWithdrawalStore(historyWithdrawalStore, hotWithdrawalStore, hotWindow);
                receipts = new Stores.CompositeReceiptStore(historyReceiptStore, hotReceiptStore, hotWindow);
                blockAccessLists = new Stores.CompositeBlockAccessListStore(historyBlockAccessListStore, hotBlockAccessListStore, hotWindow);
            }
            else
            {
                uncles = historyUncleStore;
                withdrawals = historyWithdrawalStore;
                receipts = historyReceiptStore;
                blockAccessLists = historyBlockAccessListStore;
            }

            Stores.RocksDbPromotionService promotionService = null;
            if (promotionActive && !rocks.Options.UseFreezerHistory)
            {
                promotionService = new Stores.RocksDbPromotionService(
                    rocks, hotWindow, historyBlockStore, historyTransactionStore, historyReceiptStore,
                    historyBlockAccessListStore, metadataStore, promotionMaxHistoryBlocks);
            }

            Nethereum.Freezer.Freezer freezerAppend = null;
            Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet freezerCodecs = null;
            Stores.RocksDbFilterMapsStore fmStore = null;
            Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer fmIndexer = null;
            RocksDbManager freezerHistoryRocks = null;
            string freezerHistoryDataDir = null;
            Nethereum.CoreChain.Freezer.FreezerPromotionService freezerPromotionService = null;
            Stores.RocksDbHotBlockWindowSourceAdapter freezerHotWindowAdapter = null;
            History.BulkIndexIngestor freezerIndexIngestor = null;
            Freezer.IFreezerIndexProgress freezerIndexProgress = null;
            object freezerAppendLock = null;
            if (rocks.Options.UseFreezerHistory)
            {
                freezerAppend = Nethereum.Freezer.Freezer.Open(
                    rocks.Options.FreezerMaxFileSizeBytes.HasValue
                        ? new Nethereum.Freezer.FreezerLayout(rocks.Options.FreezerHistoryDirectory, rocks.Options.FreezerMaxFileSizeBytes.Value)
                        : new Nethereum.Freezer.FreezerLayout(rocks.Options.FreezerHistoryDirectory),
                    Nethereum.Freezer.FreezerOpenMode.Append);
                freezerCodecs = new Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet();

                freezerHistoryDataDir = Path.Combine(dataDir, FreezerHistorySubDir);
                Directory.CreateDirectory(freezerHistoryDataDir);
                var freezerHistoryOptions = rocks.Options.Clone();
                freezerHistoryOptions.DatabasePath = freezerHistoryDataDir;
                freezerHistoryRocks = new RocksDbManager(freezerHistoryOptions, CatalogueScope.FreezerHistory);
                freezerIndexProgress = new Freezer.FreezerHistoryIndexProgress(freezerHistoryRocks, History.HistoryColumnFamilies.Control);

                freezerAppendLock = new object();
                freezerIndexIngestor = new History.BulkIndexIngestor(
                    freezerHistoryRocks.Database,
                    Path.Combine(freezerHistoryDataDir, "bulk-scratch"),
                    new[] { History.HistoryColumnFamilies.BlockHashIndex, History.HistoryColumnFamilies.TxHashIndex },
                    windowHead => { lock (freezerAppendLock) freezerIndexProgress.SetByHashCursor(windowHead + 1); },
                    checkpointIntervalBlocks: FreezerIndexCheckpointIntervalBlocks,
                    checkpointIntervalSeconds: BulkCheckpointIntervalSecondsDisabled,
                    maxPendingEntries: FreezerIndexCheckpointMaxPendingEntries,
                    maxResidentBytes: rocks.Options.FreezerBulkResidentCeilingBytes,
                    sortDegreeOfParallelism: rocks.Options.FreezerBulkSortDegreeOfParallelism);

                var priorBodyCursor = metadataStore.GetLastFetchedBody();
                var freezeBoundary = Freezer.FreezerAppendService.ReadTipHeightStamp(rocks) - Freezer.FreezerAppendService.FullImmutabilityThreshold;
                var reconciledBodyCursor = ReconcileBodyCursorToFreezerHead(priorBodyCursor, freezerAppend.Items, freezeBoundary);
                if (reconciledBodyCursor != priorBodyCursor)
                {
                    Console.Error.WriteLine(
                        $"[Nethereum.Freezer] resume: body cursor {priorBodyCursor} was below freezer head " +
                        $"{freezerAppend.Items}; advancing to {reconciledBodyCursor} — already-frozen blocks are " +
                        $"not re-fetched from peers.");
                    metadataStore.SetLastFetchedBody(reconciledBodyCursor);
                }

                var recentRandomIndex = promotionActive
                    ? (Nethereum.CoreChain.Freezer.IRandomKeyIndexStore)new Stores.RocksDbHotWindowRandomKeyIndexAdapter(hotWindow)
                    : new Stores.RocksDbRandomKeyIndexStore(historyManager);
                var frozenRandomIndex = new Stores.RocksDbRandomKeyIndexStore(freezerHistoryRocks);

                if (promotionActive)
                {
                    (freezerPromotionService, freezerHotWindowAdapter) = Freezer.FreezerPromotionDriver.Wire(
                        rocks, hotWindow, freezerAppend, freezerCodecs);
                }

                var freezerReceiptDeriver = new Nethereum.CoreChain.Freezer.ReceiptFieldDeriver(
                    signer, new Nethereum.CoreChain.Freezer.CancunBlobBaseFeeFractionResolver());
                var freezerDecodedClusterCache = new Nethereum.CoreChain.Freezer.DecodedClusterCache(FreezerDecodedClusterCacheBlocks);
                var freezerHistory = new Nethereum.CoreChain.Freezer.FreezerHistoryStore(
                    freezerAppend, freezerCodecs, freezerReceiptDeriver, signer, frozenRandomIndex, freezerDecodedClusterCache);
                var freezerRouter = new Stores.FreezerReadRouter(freezerAppend, recentRandomIndex, frozenRandomIndex);

                blocks = new Stores.FreezerAwareBlockStore(freezerHistory, blocks, freezerRouter);
                transactions = new Stores.FreezerAwareTransactionStore(freezerHistory, transactions, freezerRouter);
                uncles = new Stores.FreezerAwareUncleStore(freezerHistory, uncles, freezerRouter);
                withdrawals = new Stores.FreezerAwareWithdrawalStore(freezerHistory, withdrawals, freezerRouter);
                receipts = new Stores.FreezerAwareReceiptStore(freezerHistory, receipts, freezerRouter);
                blockAccessLists = new Stores.FreezerAwareBlockAccessListStore(freezerHistory, blockAccessLists, freezerRouter);

                fmStore = new Stores.RocksDbFilterMapsStore(freezerHistoryRocks);
                var fmChainView = new Nethereum.CoreChain.Freezer.FilterMaps.SegmentChainView(
                    freezerAppend, freezerCodecs,
                    maxDegreeOfParallelism: rocks.Options.FreezerBackgroundDegreeOfParallelism);
                var fmFinality = new Nethereum.CoreChain.Freezer.FilterMaps.FreezerHeadFinalitySource(freezerAppend);
                var fmParams = rocks.Options.FilterMapsIndexParams ?? Nethereum.Freezer.FilterMaps.FilterMapsParams.Default;
                fmIndexer = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer(fmStore, fmChainView, fmFinality, fmParams);

                var fmBackend = new Nethereum.Freezer.FilterMaps.FilterMapsQueryBackend(fmStore, fmParams);
                var fmMatcher = new Nethereum.Freezer.FilterMaps.FilterMapsMatcher(fmBackend);
                var fmResolver = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsLogResolver(fmStore, fmChainView, fmParams);
                var fmHotScan = new Stores.FreezerAwareBloomScan(receipts, blocks);
                var fmEngine = new Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsQueryEngine(fmStore, fmMatcher, fmResolver, fmHotScan);
                logs = new Stores.FreezerFilterMapsLogStore(fmEngine, blocks, transactions);
            }

            var bundle = new RocksDbChainStoreBundle(
                wired,
                trieNodeStore,
                stateTrieNodes,
                nodeCommitBlockSource,
                blocks,
                transactions,
                uncles,
                withdrawals,
                blockAccessLists,
                receipts,
                logs,
                metadataStore,
                diffStore,
                journalEnabled: journalOptions != null,
                dataDir: dataDir,
                rocks: rocks,
                ownsManager: ownsManager,
                bulkSave: bulk,
                nodeServing: nodeServing,
                latestProofNodeStore: latestProofNodeStore,
                healPathStore: healPathStore,
                rawFlatState: rawState,
                nodeReverseDiffStore: nodeReverseDiffStore,
                nodeHistoryFloor: nodeHistoryFloor,
                windowLayers: windowLayers,
                pendingFlushFlatOverlay: pendingFlushFlatOverlay,
                historyRocks: historyManager,
                logIndexStore: logIndexStore,
                historyDataDir: historyDataDir,
                historyBlockStore: historyBlockStore,
                historyTransactionStore: historyTransactionStore,
                historyReceiptStore: historyReceiptStore,
                hotWithdrawalStore: hotWithdrawalStore,
                hotWindow: hotWindow,
                promotionService: promotionService,
                promotionMaxHistoryBlocks: promotionMaxHistoryBlocks,
                freezerAppend: freezerAppend,
                freezerCodecs: freezerCodecs,
                fmStore: fmStore,
                fmIndexer: fmIndexer,
                freezerHistoryRocks: freezerHistoryRocks,
                freezerHistoryDataDir: freezerHistoryDataDir,
                freezerPromotionService: freezerPromotionService,
                freezerHotWindowAdapter: freezerHotWindowAdapter,
                freezerIndexIngestor: freezerIndexIngestor,
                freezerIndexProgress: freezerIndexProgress,
                freezerAppendLock: freezerAppendLock);

            return bundle;
        }

        private int _scratchBaseReclaimed;

        public IBulkFlatStateSink CreateBulkFlatSink()
        {
            var baseDir = Path.Combine(DataDir, "state-sst-scratch");
            if (Interlocked.Exchange(ref _scratchBaseReclaimed, 1) == 0)
            {
                try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
            }
            var attemptDir = Path.Combine(baseDir, Guid.NewGuid().ToString("N"));
            return new Stores.SnapFlatSstSink(_rocks, _rawFlatState, attemptDir);
        }

        public IHealNodeSink CreateHealSink()
            => _healPathStore != null
                ? (IHealNodeSink)new Stores.PathHealNodeSink(_healPathStore, _rocks)
                : new HashHealNodeSink((INodeBlobStore)TrieNodes);

        public async Task<FlatStateReconcileResult> ReconcileFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct)
        {
            if (this is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
            try
            {
                return await _recoveryService.ReconcileFlatStateAsync(stateRoot, progress, ct).ConfigureAwait(false);
            }
            finally
            {
                (State as IFlatCacheInvalidatable)?.ClearCache();
            }
        }
        public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            => _recoveryService.VerifyFlatStateAsync(stateRoot, progress, ct, sampleAccountsPerShard);

        public async Task<FlatTrieGenerationResult> GenerateTrieFromFlatAsync(byte[] expectedRoot, Action<string> progress, CancellationToken ct)
        {
            if (this is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
            try
            {
                return await new Stores.FlatStateTrieGenerator(_rocks, _rawFlatState, StateTrieNodes)
                    .GenerateAsync(expectedRoot, progress, ct).ConfigureAwait(false);
            }
            finally
            {
                (State as IFlatCacheInvalidatable)?.ClearCache();
            }
        }

        public void PruneFlatStateBeyond(IReadOnlyList<SnapSyncAccountTask> durableTasks)
        {
            try
            {
                new Stores.FlatStateTrieGenerator(_rocks, _rawFlatState, StateTrieNodes).PruneBeyond(durableTasks);
            }
            finally
            {
                (State as IFlatCacheInvalidatable)?.ClearCache();
            }
        }
        public async Task<ulong> RecoverToAsync(ulong targetBlock, FlatRecoverySource flatSource, Action<string> progress, CancellationToken ct, bool skipVerify = false)
        {
            if (this is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
            try
            {
                return await _recoveryService.RecoverToAsync(targetBlock, flatSource, progress, ct, skipVerify).ConfigureAwait(false);
            }
            finally
            {
                (State as IFlatCacheInvalidatable)?.ClearCache();
            }
        }
        public async Task<(ulong Head, bool Recovered)> EnsureConsistentHeadAsync(Action<string> progress, CancellationToken ct = default)
        {
            if (this is IAtomicBlockFlush atomicFlush)
                await atomicFlush.DrainAsync().ConfigureAwait(false);
            try
            {
                return await _recoveryService.EnsureConsistentHeadAsync(progress, ct).ConfigureAwait(false);
            }
            finally
            {
                (State as IFlatCacheInvalidatable)?.ClearCache();
            }
        }
        public System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage()
            => _recoveryService.GetPersistedDamage();
        public void ClearPersistedDamage() => _recoveryService.ClearPersistedDamage();
        public System.Collections.Generic.IReadOnlyList<byte[]> GetPersistedMissingCode()
            => _recoveryService.GetPersistedMissingCode();
        public void ClearPersistedMissingCode() => _recoveryService.ClearPersistedMissingCode();



        public Task PersistBlocksAsync(IReadOnlyList<PersistableBlock> blocks, CancellationToken ct = default)
        {
            if (blocks == null || blocks.Count == 0) return Task.CompletedTask;

            if (_freezerAppend != null)
                return AppendBlocksToFreezerThenRest(blocks, ct);

            return PersistBlocksToRocksDb(blocks, ct);
        }

        private Task PersistBlocksToRocksDb(IReadOnlyList<PersistableBlock> blocks, CancellationToken ct)
        {
            if (blocks == null || blocks.Count == 0) return Task.CompletedTask;

            if (_bulkSave != null)
            {
                ct.ThrowIfCancellationRequested();
                var enableLogIndex = _logIndexStore != null;
                var encoded = new Storage.History.HistoryBlockWrite[blocks.Count];
                System.Threading.Tasks.Parallel.For(0, blocks.Count, _persistEncodeOptions,
                    i => encoded[i] = History.HistoryBlockWriteFactory.FromPersistable(blocks[i], _provider, _serializer, enableLogIndex));
                ct.ThrowIfCancellationRequested();
                var chunk = new List<Storage.History.HistoryBlockWrite>(encoded);
                _bulkSave.WriteChunk(chunk);
                if (ReferenceEquals(_historyRocks, _rocks))
                {
                    var top = blocks[blocks.Count - 1];
                    var topNumber = top.Header.BlockNumber.ToBigInteger();
                    var metaCf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
                    var heightKey = System.Text.Encoding.UTF8.GetBytes("height");
                    var currentHeightRaw = _rocks.Get(RocksDbManager.CF_METADATA, heightKey);
                    var currentHeight = currentHeightRaw == null
                        ? System.Numerics.BigInteger.MinusOne
                        : Serialization.RocksDbSerializer.BytesToBigInteger(currentHeightRaw);
                    if (topNumber > currentHeight)
                    {
                        using (var mb = _rocks.CreateWriteBatch())
                        {
                            mb.Put(heightKey, Serialization.RocksDbSerializer.BigIntegerToBytes(topNumber), metaCf);
                            if (top.Hash != null) mb.Put(System.Text.Encoding.UTF8.GetBytes("latest_block_hash"), top.Hash, metaCf);
                            _rocks.Write(mb);
                        }
                    }
                }
                return Task.CompletedTask;
            }

            var blockStore = _historyBlockStore;
            var txStore = _historyTransactionStore;
            var receiptStore = _historyReceiptStore;
            var logStore = _logIndexStore;

            var singleDb = ReferenceEquals(_historyRocks, _rocks);
            using var historyBatch = _historyRocks.CreateWriteBatch();
            using var coreBatch = singleDb ? null : _rocks.CreateWriteBatch();
            var logBatch = singleDb ? historyBatch : coreBatch;

            foreach (var b in blocks)
            {
                ct.ThrowIfCancellationRequested();
                var blockNumber = b.Header.BlockNumber.ToBigInteger();

                blockStore.StageBlockInto(historyBatch, b.Header, b.Hash, b.Uncles, b.Withdrawals, b.Transactions?.Count ?? 0);
                if (b.Transactions != null && b.Transactions.Count > 0)
                    txStore.StageManyInto(historyBatch, b.Hash, blockNumber, b.Transactions);
                if (b.Receipts != null && b.Receipts.Count > 0)
                    receiptStore.StageManyInto(historyBatch, b.Hash, blockNumber, b.Receipts);
                if (logStore != null)
                {
                    if (b.Logs != null && b.Logs.Count > 0)
                        logStore.StageManyLogsInto(logBatch, b.Logs, b.Hash, blockNumber);
                    if (b.Bloom != null)
                        logStore.StageBlockBloomInto(logBatch, blockNumber, b.Bloom);
                }
            }
            _historyRocks.Write(historyBatch);
            if (!singleDb) _rocks.Write(coreBatch);
            return Task.CompletedTask;
        }


        public bool IsBulkSync => _bulkSave != null;

        public bool ShouldPauseHistoryWrites() => _pressureMonitor.ShouldPauseHistoryWrites();
        public string DescribeHistoryBackpressure() => _pressureMonitor.DescribeHistoryBackpressure();
        public bool ShouldPauseBackfill() => _pressureMonitor.ShouldPauseBackfill();
        public string DescribeBackfillPause() => _pressureMonitor.DescribeBackfillPause();
        public bool ShouldPauseStateWrites() => _pressureMonitor.ShouldPauseStateWrites();
        public string DescribeStateBackpressure() => _pressureMonitor.DescribeStateBackpressure();
        public Task CompactStateAsync(Action<string> progress, CancellationToken ct)
            => _pressureMonitor.CompactStateAsync(progress, ct);

        public Task CompactAllAsync(Action<string> progress, CancellationToken ct)
            => Task.Run(() =>
            {
                progress?.Invoke("full store compaction started (all column families)");
                _rocks.Compact();
                progress?.Invoke("full store compaction complete");
            }, ct);

        public void FlushStateAtomically(
            FlatStateBatch flat,
            TrieNodeSet nodes,
            ulong block, byte[] hash,
            Stores.RocksDbPathTrieNodeStore pathStore,
            IReadOnlyList<(ulong Block, byte[] Owner)> rangeWipes = null,
            IReadOnlyList<(ulong Block, TrieNodeSet Set)> perBlockJournal = null,
            IList<Nethereum.Model.Withdrawal> withdrawals = null)
        {
            var batch = StageFlushBatch(flat, nodes, block, hash, pathStore, rangeWipes, perBlockJournal,
                withdrawals, out var runPruneGate);
            WriteAndFinalizeBatch(batch, block, runPruneGate);
        }

        internal RocksDbSharp.WriteBatch StageFlushBatch(
            FlatStateBatch flat,
            TrieNodeSet nodes,
            ulong block, byte[] hash,
            Stores.RocksDbPathTrieNodeStore pathStore,
            IReadOnlyList<(ulong Block, byte[] Owner)> rangeWipes,
            IReadOnlyList<(ulong Block, TrieNodeSet Set)> perBlockJournal,
            IList<Nethereum.Model.Withdrawal> withdrawals,
            out bool runPruneGate)
        {
            var batch = _rocks.CreateWriteBatch();

            if (flat != null)
                _rawFlatState.AddBatchToWriteBatch(batch, flat);

            if (withdrawals != null)
            {
                if (ReferenceEquals(_historyRocks, _rocks))
                {
                    var target = _hotWithdrawalStore ?? (Stores.RocksDbWithdrawalStore)Withdrawals;
                    target.StageInto(batch, hash, withdrawals);
                }
                else
                {
                    using var historyBatch = _historyRocks.CreateWriteBatch();
                    ((Stores.RocksDbWithdrawalStore)Withdrawals).StageInto(historyBatch, hash, withdrawals);
                    _historyRocks.Write(historyBatch);
                }
            }

            var stagedJournalKeys = new HashSet<byte[]>(ByteArrayComparer.Current);

            if (rangeWipes != null && rangeWipes.Count > 0)
            {
                if (_nodeReverseDiffStore == null)
                    throw new InvalidOperationException("FlushStateAtomically requires node history to be enabled to stage a range wipe.");
                if (pathStore == null)
                    throw new ArgumentNullException(nameof(pathStore));
                foreach (var wipe in rangeWipes)
                    _nodeReverseDiffStore.AddRangeWipeToBatch(batch, wipe.Block, pathStore, wipe.Owner, _windowLayers, stagedJournalKeys);
            }

            if (nodes != null)
            {
                if (_nodeReverseDiffStore == null)
                    throw new InvalidOperationException("FlushStateAtomically requires node history to be enabled (TrieNodeHistoryBlocks >= 0).");
                if (pathStore == null)
                    throw new ArgumentNullException(nameof(pathStore));
                IReadOnlyList<(ulong Block, TrieNodeSet Set)> journalEntries =
                    perBlockJournal ?? new List<(ulong Block, TrieNodeSet Set)> { (block, nodes) };
                var stagedRoots = new HashSet<byte[]>(ByteArrayComparer.Current);
                foreach (var entry in journalEntries.OrderBy(e => e.Block))
                    _nodeReverseDiffStore.AddJournalRecordToBatch(batch, entry.Block, pathStore, entry.Set, _windowLayers, stagedRoots, stagedJournalKeys);
                _nodeReverseDiffStore.AddStateMoveToBatch(batch, pathStore, nodes);
            }

            ((RocksDbChainMetadataStore)Metadata).AddDurableStateToBatch(batch, block, hash);
            ((RocksDbChainMetadataStore)Metadata).AddExecutedHeadToBatch(batch, block, hash);

            runPruneGate = nodes != null;
            return batch;
        }

        internal void WriteAndFinalizeBatch(RocksDbSharp.WriteBatch batch, ulong block, bool runPruneGate)
        {
            using (batch)
            {
                _rocks.Write(batch);
                if (runPruneGate) RunPruneSweepIfDue(block);
            }
        }

        private void RunPruneSweepIfDue(ulong block)
        {
            if (_nodeHistoryFloor != null)
            {
                var q = ((long)block / NodeHistoryPruneIntervalBlocks) * NodeHistoryPruneIntervalBlocks;
                if (q > Interlocked.Read(ref _lastPrunedQuantum))
                {
                    Interlocked.Exchange(ref _lastPrunedQuantum, q);
                    _nodeReverseDiffStore.PruneBelow(_nodeHistoryFloor.FloorFor((ulong)q));
                }
            }
        }

        Task IAtomicBlockFlush.FlushBlockAsync(FlatStateBatch flat, ulong block, byte[] hash)
            => FlushBlockAsyncCore(flat, block, hash);

        private async Task FlushBlockAsyncCore(FlatStateBatch flat, ulong block, byte[] hash)
        {
            var capturing = StateTrieNodes as Stores.CapturingJournalingPathNodeStore;
            var wipes = capturing?.TakeCapturedWipes();
            var perBlockJournal = capturing?.TakeCapturedJournal();
            var nodes = capturing?.TakeCaptured();

            IList<Nethereum.Model.Withdrawal> withdrawalsToStage = null;
            if (_armedWithdrawals is { } armed && armed.Block == block)
            {
                if (armed.Withdrawals != null && armed.Withdrawals.Count > 0)
                    withdrawalsToStage = armed.Withdrawals;
                _armedWithdrawals = null;
            }

            var previous = _pendingFlush;
            await previous.ConfigureAwait(false);

            _promotionService?.PromoteDurableBlocks(Metadata.GetDurableStateBlock());
            _freezerPromotionDriver?.Drive();

            if (_windowLayers != null && perBlockJournal != null)
            {
                foreach (var entry in perBlockJournal)
                    _windowLayers.PushLayer(entry.Block, entry.Set);
            }
            if (_windowLayers != null && wipes != null)
            {
                foreach (var wipe in wipes)
                    _windowLayers.PushOwnerWipe(wipe.Block, wipe.Owner);
            }
            var pathStore = (nodes != null || (wipes != null && wipes.Count > 0)) ? _healPathStore : null;

            var batch = StageFlushBatch(flat, nodes, block, hash, pathStore, wipes, perBlockJournal,
                withdrawalsToStage, out var runPruneGate);

            ArmStagedOwnership(block, withdrawalsToStage != null);

            _pendingFlushFlatOverlay?.Populate(block, flat);

            bool dropWindowLayersOnSuccess = nodes != null && _windowLayers != null;

            _pendingFlush = Task.Run(() =>
            {
                WriteAndFinalizeWithRetry(batch, block, runPruneGate);
                if (dropWindowLayersOnSuccess)
                    _windowLayers.DropThrough(block);
                _pendingFlushFlatOverlay?.Clear(block);
            });
        }

        void IAtomicBlockFlush.DiscardCapturedBlock()
        {
            (StateTrieNodes as Stores.CapturingJournalingPathNodeStore)?.DiscardCaptured();
            _armedWithdrawals = null;
        }

        void IAtomicBlockFlush.ArmWithdrawals(ulong block, IList<Nethereum.Model.Withdrawal> withdrawals)
        {
            _armedWithdrawals = (block, withdrawals);
        }

        bool IAtomicBlockFlush.WithdrawalsFoldedFor(ulong block)
        {
            if (_stagedFlushOwnsBlock == block)
            {
                var owned = _stagedFlushOwnsWithdrawals;
                _stagedFlushOwnsBlock = null;
                _stagedFlushOwnsWithdrawals = false;
                return owned;
            }
            return false;
        }

        bool IAtomicBlockFlush.BlockOwnedByStagedFlush(ulong block) => _stagedFlushOwnsBlock == block;

        private void ArmStagedOwnership(ulong block, bool withdrawalsIncluded)
        {
            _stagedFlushOwnsBlock = block;
            _stagedFlushOwnsWithdrawals = withdrawalsIncluded;
        }

        Task IAtomicBlockFlush.DrainAsync() => _pendingFlush;

        internal Func<Exception, bool> TestTransientClassifier;

        private bool IsTransientWriteFailure(Exception ex)
        {
            if (TestTransientClassifier != null) return TestTransientClassifier(ex);
            return ex is RocksDbSharp.RocksDbException rex && RocksDbManager.IsTransientFlushStall(rex.Message);
        }

        internal void WriteAndFinalizeWithRetry(RocksDbSharp.WriteBatch batch, ulong block, bool runPruneGate)
        {
            using (batch)
            {
                const int MaxRetries = 5;
                var backoff = TimeSpan.FromMilliseconds(50);
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        _rocks.Write(batch);
                        break;
                    }
                    catch (Exception ex) when (attempt < MaxRetries && IsTransientWriteFailure(ex))
                    {
                        System.Threading.Thread.Sleep(backoff);
                        backoff += backoff;
                    }
                }
                if (runPruneGate) RunPruneSweepIfDue(block);
            }
        }

        public IBundleBatch BeginBatch()
            => new RocksDbBundleBatch(this, _rocks, snapSyncEncoder: null, syncFsync: true);

        public long FreezerHead => _freezerAppend?.Items ?? 0;
        public long ByHashIndexedHead => _freezerIndexer?.ByHashIndexedHead ?? 0;
        public long LogIndexRenderedHead => _freezerIndexer?.LogIndexRenderedHead ?? 0;
        public long LogRenderProgressBlock => _freezerIndexer?.LogRenderProgressBlock ?? 0;

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "freezer", "RocksDbChainStoreBundle.StartBackgroundFreezeIndexing — start the background freeze-index trailer")]
        public void StartBackgroundFreezeIndexing() => _freezerIndexer?.Start();

        public void FinishBulkIndexing(CancellationToken ct = default)
        {
            _freezerAppendService?.CommitPending();
            _freezerIndexer?.Finish(ct);
        }

        public (ulong Previous, ulong New) AlignByHashReindexCursorToFreezerHead()
            => _freezerIndexer?.AlignCursorToHead() ?? (0UL, 0UL);

        public void SetByHashReindexCursorForTests(ulong itemNumber) => _freezerIndexer?.SetByHashReindexCursorForTests(itemNumber);

        public int RenderFilterMapsCatchUp(CancellationToken ct = default, long? headOverride = null)
            => _freezerIndexer?.RenderFilterMaps(ct, headOverride) ?? 0;

        public void SetPressureMonitorForTests(Stores.RocksDbWritePressureMonitor monitor)
        {
            _pressureMonitor = monitor;
            _freezerIndexer?.SetPressureMonitorForTests(monitor);
        }

        public async Task ResetSnapBootstrapStateAsync(CancellationToken ct = default)
        {
            ClearPersistedDamage();
            ClearPersistedMissingCode();
            await new Stores.RocksDbStateResetService(_rocks, Metadata).ResetSnapBootstrapStateAsync(ct).ConfigureAwait(false);
            (StateTrieNodes as Stores.ICacheInvalidatable)?.ClearCache();
            (State as IFlatCacheInvalidatable)?.ClearCache();
        }

        public async Task ResetStateOnlyAsync(CancellationToken ct = default)
        {
            await new Stores.RocksDbStateResetService(_rocks, Metadata).ResetStateOnlyAsync(ct).ConfigureAwait(false);
            (StateTrieNodes as Stores.ICacheInvalidatable)?.ClearCache();
            (State as IFlatCacheInvalidatable)?.ClearCache();
        }

        public void Dispose()
        {
            _freezerIndexer?.Stop();
            _freezerIndexer?.FlushIfDirty();
            _freezerAppendService?.CommitPending();
            _freezerAppend?.Dispose();
            _freezerHistoryRocks?.Dispose();
            if (!_ownsManager) return;
            try { _pendingFlush.GetAwaiter().GetResult(); } catch { }
            (State as IDisposable)?.Dispose();
            _rocks?.Dispose();
            if (!ReferenceEquals(_historyRocks, _rocks)) _historyRocks?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_freezerIndexer != null)
                await _freezerIndexer.StopAsync().ConfigureAwait(false);
            _freezerIndexer?.FlushIfDirty();
            _freezerAppendService?.CommitPending();
            _freezerAppend?.Dispose();
            _freezerHistoryRocks?.Dispose();
            if (!_ownsManager) return;
            try { await _pendingFlush.ConfigureAwait(false); } catch { }
            (State as IDisposable)?.Dispose();
            _rocks?.Dispose();
            if (!ReferenceEquals(_historyRocks, _rocks)) _historyRocks?.Dispose();
        }
    }
}
