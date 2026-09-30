# Nethereum.CoreChain.RocksDB

A persistent database for a Nethereum node. It stores everything a chain accumulates — blocks, transactions, receipts, logs, accounts, contract code and the state trie — behind the same CoreChain interfaces an in-memory store implements, so code written against `IChainStoreBundle` runs unchanged on disk.

## What you can do with it

| Task | Reach for |
|---|---|
| Keep a full node's chain and state on disk, across restarts | `RocksDbChainStoreBundle.Open(dataDir)` |
| Store the state trie by *location* (PBSS-style path keys) instead of by hash | `RocksDbStorageOptions.PathKeyedState` |
| Serve EIP-1186 proofs and snap ranges **as of a recent block**, not just the tip | `RocksDbStorageOptions.TrieNodeHistoryBlocks` + `TrieNodeHistoryIndex` → `bundle.NodeServing` |
| Undo blocks after a reorg, from the per-block reverse-diff journal | `RocksDbNodeReverseDiffStore.MaterializingRewindTo`, driven by CoreChain's `RewindCoordinator` |
| Move ancient history out of the LSM tree into a geth-compatible append-only freezer | `RocksDbStorageOptions.UseFreezerHistory` |
| Keep recent blocks in a small hot database and the rest in a separate history one | `RocksDbStorageOptions.SplitHistoryStore` |
| Snapshot and roll back the whole database with hard-linked checkpoints | `bundle.SaveCheckpointAsync` / `RestoreCheckpointAsync` |
| Commit a batch of blocks durably, cursor last | `bundle.BeginBatch()` / `IBatchedBlockPersister.PersistBlocksAsync` |
| Bulk-load from genesis without going through the memtable | `bulkSync: true` (`SyncBulkSaveService` + `BulkIndexIngestor`) |
| Recover cleanly from a kill mid-write | `BootRecoveryGate` — the host calls it around open |

## Install

```bash
dotnet add package Nethereum.CoreChain.RocksDB
```

This assembly is **not** strong-named (`SignAssembly` is off because its RocksDB dependency is not strong-named), so it cannot be referenced from a strong-named assembly.

### Dependencies

Project references: Nethereum.CoreChain, Nethereum.DevP2P.Sync, Nethereum.Documentation, Nethereum.Freezer, Nethereum.CoreChain.Freezer. Packages: RocksDB 10.4.2, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Hosting.Abstractions, Microsoft.Extensions.Logging.Abstractions. Targets net8.0/net9.0/net10.0.

## Entry points

**Reach for `RocksDbChainStoreBundle` first.** It is the composition root: `Open(...)` builds every store this package ships, decides which storage regimes are active from `RocksDbStorageOptions`, and hands them back as properties. You almost never construct an individual store yourself — you set an option and read the store off the bundle.

| I want to... | Use |
|---|---|
| open (or create) the database | `RocksDbChainStoreBundle.Open(dataDir, journalOptions, bulkSync, storageOptions)` |
| open over a `RocksDbManager` the host already owns | `RocksDbChainStoreBundle.FromManager(rocks, dataDir, ...)` |
| read/write blocks, transactions, receipts, logs, withdrawals | `bundle.Blocks`, `.Transactions`, `.Receipts`, `.Logs`, `.Withdrawals` |
| read/write accounts, storage and code | `bundle.State` |
| read/write state-trie nodes | `bundle.StateTrieNodes` (path-keyed when enabled), `bundle.TrieNodes` (hash surface) |
| serve a proof or a snap range as of block N | `bundle.NodeServing` (needs `PathKeyedState` + `TrieNodeHistoryIndex`) |
| checkpoint, list, restore, delete | `bundle.SaveCheckpointAsync` / `ListCheckpointsAsync` / `RestoreCheckpointAsync` / `DeleteCheckpointAsync` |
| read or move the sync cursor | `bundle.Metadata` |
| choose the storage regime (path keys, history window, freezer, split) | `RocksDbStorageOptions` |

## The store bundle

`RocksDbChainStoreBundle` is a `sealed partial class` (split across `.cs`, `.Checkpoint.cs`, `.Freezer.cs` and `.Promotion.cs`). It implements `IChainStoreBundle` plus the proof-serving, batching, recovery, promotion and backpressure bundles — among them `IBatchedBlockPersister`, `IBackpressureExemptPersister`, `INodeHistoryRecoverable`, `IHistoricalProofServingBundle`, `ILatestProofServingBundle`, `IAtomicBlockFlush` and `IPromotionFloorGuard`. Open it with:

```csharp
using var bundle = RocksDbChainStoreBundle.Open(
    dataDir,
    journalOptions: HistoricalStateOptions.Default,   // flat-state value history (null = none)
    bulkSync: false,
    storageOptions: new RocksDbStorageOptions
    {
        DatabasePath = dataDir,
        PathKeyedState = true,           // location-addressed state trie
        TrieNodeHistoryBlocks = 128,     // retained node-history window
        TrieNodeHistoryIndex = true,     // key-major index (enables as-of serving)
    },
    signer: null);
```

```csharp
public static RocksDbChainStoreBundle Open(
    string dataDir, HistoricalStateOptions journalOptions = null, bool bulkSync = false,
    RocksDbStorageOptions storageOptions = null);

public static RocksDbChainStoreBundle Open(
    string dataDir, HistoricalStateOptions journalOptions, bool bulkSync,
    RocksDbStorageOptions storageOptions,
    ITransactionVerificationAndRecovery signer);

public static RocksDbChainStoreBundle FromManager(
    RocksDbManager rocks, string dataDir,
    HistoricalStateOptions journalOptions = null, bool ownsManager = true, bool bulkSync = false,
    FlatStateCache flatStateCache = null,
    RocksDbManager historyRocks = null, string historyDataDir = null);
```

- `Open(...)` creates and owns the `RocksDbManager`(s). It first completes any interrupted checkpoint restore (in `dataDir`, in the `core/` and `history/` sub-directories, and — under `UseFreezerHistory` — the `freezer-history/` one), then **detects the on-disk layout** (`DetectStorageLayout`) and reconciles it with `SplitHistoryStore` via `ResolveEffectiveSplit`: an existing single database is never orphaned by turning split on, and an existing split database forces split back on. Either override is reported on stderr.
- `FromManager(...)` builds a typed view over an externally-owned manager (`ownsManager: false` so the host controls disposal); a second overload adds the `signer`. `historyRocks`/`historyDataDir` supply the separate history database in split mode. Both `Open` and `FromManager` keep their released overloads' exact signatures — the `signer` variants are separate overloads (not an added optional parameter) to preserve binary compatibility.
- `signer` (`ITransactionVerificationAndRecovery`) is **required** when `UseFreezerHistory` is on — frozen receipts do not store the sender, so it is recovered on read.

Stores the bundle exposes: `Blocks`, `Transactions`, `Uncles`, `Withdrawals`, `BlockAccessLists`, `Receipts`, `Logs`, `Metadata`, `Diffs`, `State`, `TrieNodes` (hash surface), `StateTrieNodes` (the active state-trie store), `NodeCommitBlockSource` (the block-bracket arming site), and — in path-keyed + history mode — `NodeServing` and `LatestProofNodeStore`.

`RocksDbManager` opens one database over a column-family catalogue with workload-tuned per-CF profiles (`RocksProfiles`) sharing one block cache, and provides checkpoints, snapshots, batched writes, compaction, and an optional native rate limiter. Its `CatalogueScope` selects which catalogue a physical database opens: `Both` (legacy single DB), `Core` and `History` (the split's two databases), and `FreezerHistory` (the freezer's own narrow index-only database).

## Configuration — `RocksDbStorageOptions`

```csharp
public class RocksDbStorageOptions
{
    public string DatabasePath { get; set; } = "./chaindata";
    public long BlockCacheSize { get; set; } = 1024L * 1024 * 1024;
    public int MaxOpenFiles { get; set; } = 10000;
    public int MaxBackgroundCompactions { get; set; } = 8;
    public int MaxBackgroundFlushes { get; set; } = 2;
    public int MaxBackgroundJobs { get; set; } = 8;
    public int MaxSubcompactions { get; set; } = 4;
    public bool EnableStatistics { get; set; } = false;
    public long DbWriteBufferSize { get; set; } = 3L * 1024 * 1024 * 1024;
    public long MaxTotalWalSize { get; set; } = 512 * 1024 * 1024;
    public long BytesPerSync { get; set; } = 1 * 1024 * 1024;
    public StoragePreset Preset { get; set; } = StoragePreset.MainnetFull;
    public bool RateLimiterEnabled { get; set; } = false;
    public long RateLimiterBytesPerSecond { get; set; } = 128L * 1024 * 1024;
    public bool BufferTrieWrites { get; set; } = false;
    public bool PathKeyedState { get; set; } = false;
    public int TrieNodeHistoryBlocks { get; set; } = -1;
    public bool TrieNodeHistoryIndex { get; set; } = false;
    public bool SplitHistoryStore { get; set; } = false;
    public int HotWindowBlocks { get; set; } = 128;
    public bool PromotionEnabled { get; set; } = false;
    public bool EnableLogIndex { get; set; } = false;
    public bool UseFreezerHistory { get; set; } = false;
    public string FreezerHistoryDirectory { get; set; }
    public bool BackgroundFreezeIndexing { get; set; } = false;
    public int FreezerBackgroundDegreeOfParallelism { get; set; } = 0;
    public FilterMapsParams? FilterMapsIndexParams { get; set; }
    public long? FreezerMaxFileSizeBytes { get; set; }
    public long FreezerCommitCadenceBlocks { get; set; } = 32_768;
    public long FreezerBulkResidentCeilingBytes { get; set; } = BulkIndexIngestor.DefaultResidentCeilingBytes;
    public int FreezerBulkSortDegreeOfParallelism { get; set; } = 0;
    public bool SkipBootReconcile { get; set; } = false;

    public void Validate();
    public RocksDbStorageOptions Clone();
}

public enum StoragePreset
{
    MainnetFull,
    MainnetArchive,
    AppChainSmall,
    SyncBulk
}
```

`TrieNodeHistoryBlocks` is a tri-state: `-1` off, `0` archive (never prune), `N > 0` retain an N-block window. `Validate()` rejects anything below `-1`, rejects `TrieNodeHistoryIndex` without history (`>= 0`), and rejects history without `PathKeyedState` — the hash store cannot journal path-keyed nodes.

Combinations the bundle refuses outright, at `FromManager` time:

| Combination | Why it throws |
|---|---|
| `PromotionEnabled` + split history | promotion needs one physical database |
| `PromotionEnabled` + `EnableLogIndex` | promotion does not manage reorg-able log-index rows |
| split + `bulkSync` + `EnableLogIndex` | the bulk firehose's block-bloom/log-index extras are core-scope CFs it cannot reach on a history-only manager |
| `UseFreezerHistory` + split history | the freezer already *is* the history backend, so a second full history database would sit alongside it unused |
| `UseFreezerHistory` without `FreezerHistoryDirectory` | there is nowhere to write the archive |
| `UseFreezerHistory` without a `signer` | frozen receipts need sender recovery on read |

`UseFreezerHistory` **combines** with `PromotionEnabled`: with both on, the freezer's own follow-promotion driver (`FreezerPromotionService`, wrapped by `FreezerPromotionDriver`) drives the hot window instead of `RocksDbPromotionService`.

`HistoricalStateOptions` (defined in Nethereum.CoreChain) governs the separate **flat-state value** reverse-diff journal: `Default` (256-block window, pruning on), `FullArchive` (unbounded, no pruning), `DevChainDefault` (128). Node history may not outlive it — `TrieNodeHistoryBlocks` must be `<= MaxHistoryBlocks` (or both 0), because a rewind cannot reconstruct node state beyond the value journal it replays against.

## Key-path (path-keyed) state storage

The classic Ethereum trie store is **content-addressed**: a node is stored under `keccak(rlp(node))`, so every edit writes a new key and the old one lingers. `RocksDbPathTrieNodeStore` instead keys a node by **where it sits in the trie** — its `(Owner, Path)`:

- the account trie in `CF_STATE_TRIE_ACCOUNT` (`"state_trie_account"`), key = the node's path;
- each contract's storage trie in `CF_STATE_TRIE_STORAGE` (`"state_trie_storage"`), key = `owner ‖ path`, where `owner = keccak(address)`.

Because the key is the location, an update **overwrites in place** rather than appending: the live state trie stays a fixed number of keys, and node history becomes an explicit, bounded thing (below) instead of an accidental byproduct. The `owner` prefix is what keeps two contracts' identical storage paths from colliding, and it is what makes a whole contract's subtree deletable in one range operation.

The store:

- **skips embedded nodes** shorter than 32 bytes — they are inlined in their parent and have no independent key;
- **verifies `keccak(blob)` against the reference hash on every read**, throwing on a mismatch, so a location-addressed read is as safe as a content-addressed one;
- gates `ContainsKey(stateRoot)` by hashing the account-root node;
- wipes a contract's whole storage subtree with `DeleteRange(owner)` (SELFDESTRUCT), via `IContractStorageWipeable`.

The legacy content-addressed store, `RocksDbTrieNodeStore` (in `CF_TRIE_NODES`), remains available as the `TrieNodes` handle; hash mode is byte-identical to prior behaviour and leaves `TrieNodes == StateTrieNodes`.

## Node-history reverse-diff journal

`RocksDbNodeReverseDiffStore` records a **per-block reverse diff** of the path-keyed trie so the node can rewind and serve historical state:

```csharp
public RocksDbNodeReverseDiffStore(RocksDbManager manager, bool buildKeyMajorIndex = false);

public void RecordAndCommit(ulong block, RocksDbPathTrieNodeStore pathStore, TrieNodeSet set);
public void RecordAndDeleteRange(ulong block, RocksDbPathTrieNodeStore pathStore, byte[] owner);
public byte[] FindBlobAsOf(byte[] owner, byte[] path, ulong targetN);
public ulong? FindBlockByStateRoot(byte[] stateRoot);
public NodeRewindStats MaterializingRewindTo(ulong targetBlock);
public void PruneBelow(ulong floorBlock);
```

- **`RecordAndCommit`** reads each changed node's previous on-disk blob, writes the block-major reverse-diff row to `CF_NODE_HISTORY` (string value `"node_band_log"`), and appends the forward state move to the *same* `WriteBatch` — the diff and the state move land atomically. **First-write-wins** per `(block, owner, path)`, so a block's several armed commits (transactions, then the end-of-block system calls / finalisation) all record the value at the *previous* block boundary.
- **`FindBlobAsOf`** returns a node's value as of block N via a single seek on the key-major index (`CF_NODE_HISTORY_INDEX`, when `TrieNodeHistoryIndex` is on).
- **`FindBlockByStateRoot`** resolves a state root to its block via `CF_STATE_ROOT_INDEX` (the account-root node hashes to the block's state root; first-write-wins).
- **`PruneBelow`** drops history below the retention floor with a bounded prefix sweep.

`JournalingPathNodeStore` is the write-path decorator: when armed by `NodeCommitBlockSource` (a normal followed block) it journals + interval-prunes; when unarmed (genesis, rewind-patch, concurrent snap bootstrap) it commits plainly without journaling. `CapturingJournalingPathNodeStore` is the capture variant.

## Historical / as-of serving

When path-keyed state, node history, and the key-major index are all on, the bundle exposes `HistoricalNodeServing` (implements `ISnapNodeStoreSelector` and `IHistoricalProofCapable`):

- The **serve head** is the committed-*state* tip (`Metadata.GetLastBlock()`), not the header tip (headers run ahead during sync).
- **`ProofServiceAsOf(blockNumber)`** and **`ResolveForRootAsync(stateRoot)`** return an `AsOfBlockNodeStore` — a read-only overlay that resolves each node via `FindBlobAsOf` (present → that blob, absent-sentinel → didn't exist, unchanged → latest path store), gated by the retention **floor** (`FixedWindowFloorPolicy`, `head - N`). Requests below the floor throw `StateNotAvailableException` (JSON-RPC −32000).
- `PathKeyedProofNodeStore` is the latest-only adapter used for tip proofs (`LatestProofNodeStore`). Both extend `ReadOnlyRootGatedNodeStore`, which provides the root-presence gate and rejects mutation.

## Freezer / ancient store

`UseFreezerHistory` moves immutable history out of the LSM tree into `Nethereum.Freezer`'s geth-compatible append-only files under `FreezerHistoryDirectory`. After that, **a block lives in exactly one tier**: number `<` the freezer's item count is frozen; everything else is the mutable RocksDB "recent" band. The frozen by-hash and EIP-7745 filtermaps indexes live in their own narrow database (`FreezerHistorySubDir` = `"freezer-history"`, `CatalogueScope.FreezerHistory`, holding only `block_hash_index` / `tx_hash_index` / `log_filter_maps` / `control`), opened internally — independent of `SplitHistoryStore`'s history database.

`FreezerReadRouter` is the single source of truth for the routing decision. Its by-hash lookup consults two indexes, recent-first, so a block's hash resolves both before and after it is frozen:

```csharp
public FreezerReadRouter(Freezer freezer, IRandomKeyIndexStore recentIndex, IRandomKeyIndexStore frozenIndex);
public long FrozenCount();
public bool TryResolveNumber(byte[] hash, out long number);
public bool TryResolveTxLocation(byte[] txHash, out long blockNumber, out int txIndex);
```

Six thin `FreezerAware*Store` wrappers apply it — `FreezerAwareBlockStore`, `FreezerAwareTransactionStore`, `FreezerAwareUncleStore`, `FreezerAwareWithdrawalStore`, `FreezerAwareReceiptStore` and `FreezerAwareBlockAccessListStore` — each routing a read to the frozen `FreezerHistoryStore` or to the recent RocksDB store, and always writing to the recent side.

Because a frozen receipt does not store its sender or its effective gas price, `ReceiptFieldDeriver` (with the supplied `signer` and a Cancun blob-base-fee resolver) re-derives those fields on read, and a `DecodedClusterCache` keeps recently decoded blocks hot.

### Derived-index build — the background trailer

The freezer's derived indexes — the by-hash reverse lookups and the EIP-7745 filtermaps log index — are built by `FreezerBackgroundIndexer`, the single writer of those CFs. It can run in two modes:

- **Inline** (default): each freeze batch renders its indexes as blocks freeze, on the persist drain.
- **Background trailer** (`BackgroundFreezeIndexing = true`): the index build moves onto a standing loop that chases the freezer head, so the drain does only freezer-append + cursor-advance. Start it after construction:

```csharp
public void StartBackgroundFreezeIndexing();   // begins the background freeze-index trailer (FreezerBackgroundIndexer.Start)
```

Both modes write through the SAME bulk-SST engine (`BulkIndexIngestor`, sorted SST ingest — never a per-batch memtable `WriteBatch`), which is what keeps the index build's compaction cost bounded. The trailer is also pausable independently of the freezer files: it waits out a compaction backlog on the index CFs (`RocksDbWritePressureMonitor.ShouldPauseFreezerIndexing`) while the freezer keeps filling. During fast sync the trailer can lag the freeze head, so a by-hash lookup for a frozen-but-not-yet-indexed block returns null until it catches up (the frontier is reported, never silent); `BackgroundFreezeIndexing` is off by default so the established freeze⇒indexed-synchronously contract holds for every existing consumer.

Logs are served by `FreezerFilterMapsLogStore` over the filtermaps index: `FilterMapsQueryEngine` answers `eth_getLogs` from the rendered maps, and `FreezerAwareBloomScan` covers the un-indexed hot tail. `FilterMapsIndexParams` overrides the map geometry and exists only so a test can use tiny params — production always uses `FilterMapsParams.Default`, geth's real geometry.

## Hot window vs history

Two options split the write path between a small, hot tip band and a bulk history tier. The hot window is active when **either** `SplitHistoryStore` or `PromotionEnabled` is on.

```csharp
public static class HotWindowColumnFamilies
{
    public static IReadOnlyList<(string Name, LiveCfProfile Profile)> Catalogue { get; }
}
```

Its column families are `CF_HOT_BLOCK_HEADER`, `CF_HOT_BLOCK_META`, `CF_HOT_BLOCK_HASH_INDEX`, `CF_HOT_TX_BODY`, `CF_HOT_TX_HASH_INDEX`, `CF_HOT_RECEIPT_BODY` and `CF_HOT_BLOCK_ACCESS_LIST` — `LocationSequential` for the bodies, `SecondaryIndex` for the hash indexes.

- **`SplitHistoryStore`** opens **two physical databases** under `dataDir` — a `core/` manager (`CatalogueScope.Core`) and a `history/` manager (`CatalogueScope.History`) — so bulk history writes cannot stall the hot core. `RocksDbHistoryStore` owns the history side, over its own column families:

```csharp
public static class HistoryColumnFamilies
{
    public const string TxBody = "tx_body";
    public const string ReceiptBody = "receipt_body";
    public const string BlockHeader = "block_header";
    public const string BlockMeta = "block_meta";
    public const string BlockAccessList = "block_access_list";
    public const string TxHashIndex = "tx_hash_index";
    public const string BlockHashIndex = "block_hash_index";
    public const string LogBody = "log_body";
    public const string LogAddressIndex = "log_address_index";
    public const string LogTopicIndex = "log_topic_index";
    public const string LogFilterMaps = "log_filter_maps";
    public const string ForkHeaders = "fork_headers";
    public const string Control = "control";
    public enum HistoryCfProfile { Bulk, Control, LiveIndex, UniversalIndex }
    public static readonly IReadOnlyList<(string Name, HistoryCfProfile Profile)> Catalogue;
    public static readonly IReadOnlyList<(string Name, HistoryCfProfile Profile)> FreezerHistoryCatalogue;
}
```

  In split mode the hot window **write-throughs** to history and evicts on write (`evictOnWrite: true`), so history is always complete and the hot band only accelerates tip reads. (`FreezerHistoryCatalogue` is the narrow `block_hash_index`/`tx_hash_index`/`log_filter_maps`/`control` slice the freezer's own database opens.)

- **`PromotionEnabled`** keeps a single database and instead *defers* the move: blocks are written only to the hot window, and `RocksDbPromotionService.PromoteDurableBlocks(durableHead)` copies them into the history CFs once they fall `MaxHistoryBlocks` behind the durable head, advancing a persisted promotion cursor. `ReconcilePromotionOnBoot(durableHead)` initialises that cursor if unset, promotes what is owed, and sweeps the hot band at or below it. In promotion mode the hot window's size is derived from `HistoricalStateOptions.MaxHistoryBlocks` (falling back to `HotWindowBlocks`), so the two windows cannot disagree; the bundle also exposes a `PromotionFloor(currentHead)` guard (`IPromotionFloorGuard`) so nothing reads below what has been promoted.

Reads are unified by the `Composite*Store` family (`CompositeBlockStore`, `CompositeTransactionStore`, `CompositeReceiptStore`, `CompositeLogStore`, `CompositeUncleStore`, `CompositeWithdrawalStore`, `CompositeBlockAccessListStore`): hot first, then history.

## Reorg rewind

The rewind orchestration lives in the sibling **Nethereum.CoreChain** package and operates on this bundle. `RewindCoordinator.RewindToAsync(target, RewindPolicy.JournalFirstThenSnapshot)` selects its path from the store regime:

- **Path-keyed state** rewinds in-process through the node-history journal. Because `RocksDbChainStoreBundle` implements `INodeHistoryRecoverable`, the coordinator drives `RecoverToAsync(target, FlatRecoverySource.ReplayJournal, progress, ct)` to roll the trie, flat state and metadata cursor back to the target, verifying the trie is whole at the target before it moves the cursor. `RocksDbNodeReverseDiffStore.MaterializingRewindTo` is the node-level step, reporting `NodeRewindStats` (`TargetBlock`, `MaxHistoryBlock`, `MinHistoryBlock`, `EntriesApplied`, `Puts`, `Deletes`).
- **Fallback ladder** — on a journal hole the coordinator retries with `FlatRecoverySource.ReconcileFromTrie`, rebuilding flat state from the restored trie (the trie is the source of truth); and when neither node history nor the journal can cover the target (or `policy` is `RewindPolicy.SnapshotOnly`), it recommends the nearest whole-database checkpoint at or below the target, which the host restores and reopens. `RewindPolicy` is `JournalFirstThenSnapshot` / `JournalOnly` / `SnapshotOnly`.
- **Hash-keyed state** rewinds through the flat-state value journal (`StateRewindService`); any historical root already resolves in hash mode, so no trie patch is needed.

After an in-process rewind, the coordinator truncates the archive stores — transactions, receipts, logs, uncles, withdrawals, block-access-lists, headers — for every block above the new head.

## Boot recovery

```csharp
public sealed class BootRecoveryGate
{
    public const string RestoreRequestFileName = "restore-checkpoint.request";
    public const string CleanShutdownMarkerFileName = "clean-shutdown.ok";

    public BootRecoveryGate(ILogger logger);
    public void ApplyPendingRestore(string dataDir, string freezerHistoryDirectory = null, bool promotionEnabled = false);
    public void EnsureConsistentOrEscalate(RocksDbChainStoreBundle bundle, string dataDir);
    public bool RecordRestoreRequest(string dataDir, ulong blockNumber);
    public void MarkCleanShutdown(string dataDir);
}
```

A host calls `ApplyPendingRestore` before opening the database (to honour a restore requested by a previous run), `EnsureConsistentOrEscalate` after opening and before serving, and `MarkCleanShutdown` on an orderly stop — an absent clean-shutdown marker is what turns the next boot into a consistency check rather than a straight start. On an unrecoverable torn head `EnsureConsistentOrEscalate` records a checkpoint-restore request and throws (a bounded escalation that cannot loop). `RocksDbRecoveryService` and `RocksDbStateResetService` are the underlying operations; `RocksDbLifetimeService` wires the whole lifecycle into a generic host.

## Other subsystems

- **Bulk SST ingest** — `SyncBulkSaveService` (`bulkSync: true`): a from-genesis firehose that builds hash indexes via sorted `SstFileWriter` + `IngestExternalFiles` (never the memtable), with per-chunk durable progress (`DefaultCheckpointIntervalBlocks` 4096, `DefaultCheckpointIntervalSeconds` 30). The index-accumulate/checkpoint engine is extracted into `BulkIndexIngestor` (`DefaultCheckpointMaxPendingEntries` 2,000,000), reused by both the firehose and the freezer's by-hash index.
- **Buffered trie writer** — `BufferedTrieStorage` (`BufferTrieWrites: true`): coalesces per-node writes into one `WriteBatch` per flush; single-threaded follow path only.
- **Checkpoints** — native hard-linked RocksDB checkpoints under `.cp/<blockNumber>/`, managed by `RocksDbCheckpointManager`; `SaveCheckpointAsync` / `ListCheckpointsAsync` / `RestoreCheckpointAsync` / `DeleteCheckpointAsync` / `ResolveCheckpointSnapshotPath` / `ExportDatabaseAsync`, plus `CompleteInterruptedRestore` and `RestoreFromCheckpointDir` on open.
- **Batched persistence** — `IBatchedBlockPersister.PersistBlocksAsync` and `BeginBatch()` (`IBundleBatch`, `RocksDbBundleBatch`) commit block data durably first, then the metadata cursor last (cursor never ahead of data).
- **Write pressure** — `RocksDbWritePressureMonitor` is the pause policy the bundle forwards its backpressure interfaces to: it watches per-pipeline compaction debt / level-0 backlog, plus a process-wide engine-pressure safety net, an operator pause file, and a separate freezer-history compaction valve, holding the hysteresis state. The bundle constructs one instance and answers `IHistoryWriteBackpressure` / `IStateWriteBackpressure` / `IBackfillPauseControl` through it, so the sync pipeline pauses when the LSM tree falls behind.
- **History store** — `RocksDbHistoryStore` (`IHistoryStore`) is the location-keyed follower (tip) history write path: bodies keyed on `(blockNumber, txIndex)` / `blockNumber` with only the small tx/block hash indexes hash-keyed.
- **Binary trie** (EIP-7864) stores are also present: `RocksDbBinaryTrieNodeStore` / `RocksDbBinaryTrieStorage` over `CF_BINARY_TRIE_NODES`, `CF_BINARY_TRIE_DEPTH_IDX` and `CF_BINARY_TRIE_ADDR_STEMS`.

### Extracted adapters and finality source

The follow-promoting freezer path is wired from a few small public adapters:

- **`ImmutabilityDepthFinalitySource`** (`IFinalitySource`) — reports `tip - immutabilityThreshold` (the reorg-safe depth) so follow-promotion and bulk-freeze freeze identical blocks.
- **`RocksDbHotBlockWindowSourceAdapter`** (`IHotBlockWindowSource`, `IHotBlockWindowEvict`) — `FreezerPromotionService`'s concrete hot-window source/evict over `RocksDbHotBlockWindowStore`.
- **`RocksDbHotWindowRandomKeyIndexAdapter`** (`IRandomKeyIndexStore`) — `FreezerReadRouter`'s "recent" by-hash index when promotion routes the tip band through the hot window's own hash-index CFs.

## Usage examples

**Open the bundle, write an account, and checkpoint the state** — extracted from `RocksDbChainStoreBundleTests.SaveAndRestoreCheckpoint_RoundTrip_PreservesStateAtCheckpoint` (`tests/Nethereum.CoreChain.RocksDB.UnitTests/RocksDbChainStoreBundleTests.cs`). Note the argument order is `(blockNumber, stateRoot, blockHash)`:

```csharp
using var bundle = RocksDbChainStoreBundle.Open(dataDir);

await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
var cp = await bundle.SaveCheckpointAsync(50, stateRoot, blockHash);   // ChainCheckpoint

// later, after a mutation, restore the on-disk snapshot and reopen:
var snapshotDir = Path.Combine(dataDir, ".cp", "000000000050");
Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(snapshotDir, dataDir);
```

```csharp
public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default);
```

**Path-keyed store: account + storage tries round-trip after reopen** — extracted from `RocksDbPathKeyedStateRoundTripTests.PathState_Account_And_Storage_RoundTrip_After_Reopen` (`tests/Nethereum.CoreChain.RocksDB.UnitTests/RocksDbPathKeyedStateRoundTripTests.cs`). The two tries that share the store are separated by their `owner` — `PatriciaTrie` has both a store-only and a store-plus-owner constructor, and `LoadFromStorage` reloads at a past root:

```csharp
var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PathKeyedState = true });
var store = new RocksDbPathTrieNodeStore(mgr);

var accountTrie = new PatriciaTrie(store, hashProvider);                 // account trie: owner null
var storageTrie = new PatriciaTrie(store, keccakOfAddr, hashProvider);   // storage trie: owner = keccak(addr)
// ... Put + SaveNodesToStorage + store.Flush() ...

var reopened  = new RocksDbPathTrieNodeStore(mgr);
var reloaded  = PatriciaTrie.LoadFromStorage(accountRoot, reopened);            // account: owner null
var reloadedS = PatriciaTrie.LoadFromStorage(storageRoot, reopened, keccakOfAddr); // storage: owner keyed
```

Reading a correctly-stored blob back through a reference carrying a *wrong* hash makes verify-on-read compare `keccak(blob)` to the reference hash and throw `InvalidOperationException` — a location-addressed read is as safe as a content-addressed one.

**Enable as-of node serving** — extracted from `RocksDbBundleNodeServingTests` (`"Enable historical state serving with path-keyed state, node history and index"`):

```csharp
using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions
{
    PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
});
// bundle.NodeServing is now non-null; serves proofs/snap ranges as-of the retained window.
```

**Enable the freezer** — extracted from `FreezerReadWiringTests.Given_FrozenBlock_When_GetByNumber_Then_ServedFromFreezer` (`tests/Nethereum.CoreChain.RocksDB.UnitTests/FreezerReadWiringTests.cs`):

```csharp
var options = new RocksDbStorageOptions
{
    UseFreezerHistory = true,
    FreezerHistoryDirectory = Path.Combine(dir, "freezer"),
};
using var bundle = RocksDbChainStoreBundle.Open(
    dir, journalOptions: null, bulkSync: false, storageOptions: options,
    signer: new TransactionVerificationAndRecoveryImp());   // required by UseFreezerHistory

// bundle.Blocks/.Transactions/.Receipts route through the FreezerAware*Store wrappers:
// a frozen block reads back from the archive, a recent one from RocksDB, over the one public surface.
```
