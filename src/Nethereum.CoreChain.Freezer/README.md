# Nethereum.CoreChain.Freezer

The CoreChain layer over `Nethereum.Freezer`. Where `Nethereum.Freezer` is the domain-free byte-level
ancient store (bytes in, bytes out), this package is where Nethereum's block types meet the freezer
format and where a running node uses the freezer as its finalized-history tier:

- **Codecs** — turn `BlockHeader`, transactions, receipts, withdrawals and EIP-7928 block access lists
  into the raw geth-compatible item bytes each freezer table stores, and back.
- **Receipt-field reconstruction** — geth trims receipts to three fields before freezing them; this
  package strips them on write and rebuilds `gasUsed`, `logs`, `bloom`, `txHash`, effective gas price,
  contract address and blob fields on read, so a receipt-trie root built from the reconstructed receipts
  matches the header's `ReceiptsRoot`.
- **The finality-gated write path** — `FreezerPromotionService` moves finalized hot-window blocks into
  the append-only freezer.
- **The read-tier adapter** — `FreezerHistoryStore` exposes the freezer through the six CoreChain
  node-store interfaces a composite store reads like any other tier.
- **The EIP-7745 filtermaps log index** — the render/query stack that indexes finalized blocks into the
  `fm-` log index and answers `eth_getLogs` over it, built on the pure filtermaps core in
  `Nethereum.Freezer`.

## Installation

```bash
dotnet add package Nethereum.CoreChain.Freezer
```

### Dependencies

Nethereum.Freezer, Nethereum.CoreChain, Nethereum.Documentation (direct project references). Targets
net8.0/net9.0/net10.0.

## Codecs — domain block data ↔ freezer item bytes

`FreezerCodecSet` bundles the five typed per-stream codecs, one per freezer table. Each implements the
base package's `IItemCodec<T>` (`Nethereum.Freezer`) — the RLP/raw item shape only; the freezer's own
per-table `SnappyItemCodec` applies snappy compression, so these codecs are handed already-decompressed
item bytes.

```csharp
public sealed class FreezerCodecSet
{
    public IItemCodec<BlockHeader> Headers { get; }
    public IItemCodec<BlockBodyCluster> Bodies { get; }
    public IItemCodec<IReadOnlyList<ReceiptForStorage>> Receipts { get; }
    public IItemCodec<byte[]> Hashes { get; }
    public IItemCodec<byte[]> Bals { get; }

    public FreezerCodecSet();
}
```

| Codec | Table | Item shape |
| --- | --- | --- |
| `HeaderItemCodec` | `headers` | `RLP(header)`, via `Nethereum.Model.BlockHeaderEncoder` |
| `BodyClusterItemCodec` | `bodies` | `RLP([txs, uncles, withdrawals])`; withdrawals `null` pre-Shanghai vs empty-but-present post-Shanghai is preserved |
| `ReceiptsItemCodec` | `receipts` | `RLP([]ReceiptForStorage)` — geth's trimmed 3-field storage receipt, no bloom, no per-tx type byte |
| `HashesItemCodec` | `hashes` | the raw 32-byte block hash, uncompressed |
| `BalItemCodec` | `bals` | raw EIP-7928 block-access-list bytes, passthrough (empty placeholder pre-activation) |

`BodyClusterItemCodec` encodes/decodes a `BlockBodyCluster`:

```csharp
public sealed class BlockBodyCluster
{
    public IReadOnlyList<ISignedTransaction> Txs { get; }
    public IReadOnlyList<BlockHeader> Uncles { get; }
    public IReadOnlyList<Withdrawal> Withdrawals { get; }   // null preserved pre-Shanghai

    public BlockBodyCluster(
        IReadOnlyList<ISignedTransaction> txs,
        IReadOnlyList<BlockHeader> uncles,
        IReadOnlyList<Withdrawal> withdrawals);
}
```

## Receipt reconstruction

Geth's freezer stores a **trimmed** receipt — `ReceiptForStorage`, just three fields — and re-derives
everything else on read. `ReceiptForStorage` is what `ReceiptsItemCodec` reads and writes:

```csharp
public sealed class ReceiptForStorage
{
    public byte[] PostStateOrStatus { get; }
    public BigInteger CumulativeGasUsed { get; }
    public IReadOnlyList<Log> Logs { get; }

    public ReceiptForStorage(byte[] postStateOrStatus, BigInteger cumulativeGasUsed, IReadOnlyList<Log> logs);
}
```

`ReceiptFieldDeriver` rebuilds the full receipt from the trimmed one plus the sibling header and body —
geth's `Receipt.DeriveFields`. The reconstructed receipts rebuild the header's `ReceiptsRoot` exactly:

```csharp
public sealed class ReceiptFieldDeriver
{
    public ReceiptFieldDeriver(ITransactionVerificationAndRecovery signer, IBlobBaseFeeFractionResolver blobFraction);

    public IReadOnlyList<DerivedReceipt> Derive(
        BlockHeader header,
        BlockBodyCluster body,
        IReadOnlyList<ReceiptForStorage> stored);
}
```

Each `DerivedReceipt` carries the fields geth's trimmed form drops, per log location included:

```csharp
public sealed class DerivedReceipt
{
    public byte[] PostStateOrStatus { get; }
    public BigInteger CumulativeGasUsed { get; }
    public BigInteger GasUsed { get; }
    public byte[] Bloom { get; }
    public IReadOnlyList<DerivedLog> Logs { get; }
    public byte[] TxHash { get; }
    public TransactionType TransactionType { get; }
    public EvmUInt256 EffectiveGasPrice { get; }
    public string ContractAddress { get; }        // create-tx only, else null
    public BigInteger? BlobGasUsed { get; }        // EIP-4844, else null
    public EvmUInt256? BlobGasPrice { get; }       // EIP-4844, else null
}

public sealed class DerivedLog
{
    public Log Log { get; }
    public byte[] BlockHash { get; }
    public long BlockNumber { get; }
    public byte[] TxHash { get; }
    public int TxIndex { get; }
    public int LogIndex { get; }                   // block-cumulative, not per-tx
}
```

The effective gas price is fork-aware; the blob base-fee fraction is resolved through a seam so a
fork-aware resolver can be swapped in:

```csharp
public interface IBlobBaseFeeFractionResolver
{
    EvmUInt256 FractionForBlock(BlockHeader header);
}
```

`CancunBlobBaseFeeFractionResolver` applies Cancun's fraction; it is the default resolver.

```csharp
var deriver = new ReceiptFieldDeriver(
    new TransactionVerificationAndRecoveryImp(),
    new CancunBlobBaseFeeFractionResolver());

var derived = deriver.Derive(header, body, stored);
BigInteger gasUsed = derived[0].GasUsed;                 // rebuilt from the cumulative telescoping sum
EvmUInt256 effectiveGasPrice = derived[0].EffectiveGasPrice;
```

## Writing history — `FreezerPromotionService`

`FreezerPromotionService` is the finality-gated write path: it appends finalized hot-window blocks to
the freezer in lockstep, commits them as one durability unit, then indexes what is now durably frozen.
The freeze boundary is real finality — a block is promoted only once it can never be reorged out.

```csharp
public sealed class FreezerPromotionService
{
    public FreezerPromotionService(
        Freezer freezer,
        FreezerCodecSet codecs,
        IHotBlockWindowSource hot,
        IFinalitySource finality,
        IRandomKeyIndexStore indexes);

    public PromotionResult PromoteFinalizedBlocks(long maxBlocksPerCall = long.MaxValue);
}

public readonly struct PromotionResult
{
    public long PromotedCount { get; }
    public long NewFreezerItems { get; }
}
```

```csharp
using var freezer = Freezer.Open(new FreezerLayout(ancientDirectory), FreezerOpenMode.Append);
var service = new FreezerPromotionService(freezer, new FreezerCodecSet(), hotWindow, finality, indexes);

PromotionResult result = service.PromoteFinalizedBlocks();   // freezes every hot block ≤ finalized head
long promoted = result.PromotedCount;
long freezerHead = result.NewFreezerItems;
```

### The seams promotion reads through

Promotion reads not-yet-frozen blocks from a hot-window source and measures the freeze boundary against
a finality source. Both are interfaces so mainnet and an AppChain can each supply their own:

```csharp
public interface IHotBlockWindowSource
{
    long HotTipNumber { get; }
    HotBlock ReadHotBlock(long blockNumber);
}

public interface IFinalitySource
{
    long FinalizedBlockNumber { get; }
}
```

`IHotBlockWindowEvict` (`EvictAtOrBelow(long number)`) is the write-side half of the hot-window seam,
called once promotion has durably frozen through a block. A `HotBlock` is the typed source cluster
promotion consumes — full consensus receipts, which promotion strips to `ReceiptForStorage`:

```csharp
public sealed class HotBlock
{
    public BlockHeader Header { get; }
    public byte[] BlockHash { get; }
    public BlockBodyCluster Body { get; }
    public IReadOnlyList<Receipt> Receipts { get; }
    public byte[] BalRlp { get; }

    public HotBlock(BlockHeader header, byte[] blockHash, BlockBodyCluster body,
        IReadOnlyList<Receipt> receipts, byte[] balRlp);
}
```

### By-hash index — `IRandomKeyIndexStore`

The freezer is item-number-addressed only; block-hash and tx-hash lookups need a key-value index
alongside it. Promotion populates it; `FreezerHistoryStore` reads through it.

```csharp
public interface IRandomKeyIndexStore
{
    bool TryGetBlockNumberByHash(byte[] blockHash, out long number);
    bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex);
    void PutBlockHash(byte[] hash, long number);
    void PutTxLocation(byte[] txHash, long blockNumber, int txIndex);
    void RemoveBlock(long number);
}
```

- `InMemoryRandomKeyIndexStore` — thread-safe in-memory implementation for tests and small (AppChain)
  deployments.
- `NoOpRandomKeyIndexStore` — discards writes and resolves no reads, for a driver whose by-hash writes
  must land nowhere because another component owns the real by-hash index.

## Reading history — `FreezerHistoryStore`

`FreezerHistoryStore` is the drop-in history-tier adapter: it presents the append-only freezer through
six CoreChain node-store interfaces (`Nethereum.CoreChain.Storage`), so a composite store reads frozen
history like any other tier. It composes the freezer, the codecs, `ReceiptFieldDeriver`, the by-hash
index and a decode-once cache.

```csharp
public sealed class FreezerHistoryStore :
    IBlockStore, ITransactionStore, IReceiptStore, IBlockAccessListStore, IUncleStore, IWithdrawalStore
{
    public FreezerHistoryStore(
        Freezer freezer,
        FreezerCodecSet codecs,
        ReceiptFieldDeriver deriver,
        ITransactionVerificationAndRecovery signer,
        IRandomKeyIndexStore hashIndexes,
        DecodedClusterCache cache);
}
```

```csharp
var store = new FreezerHistoryStore(freezer, codecs, deriver, signer, indexes, new DecodedClusterCache(1024));

BlockHeader header = await store.GetByNumberAsync(0);
List<ISignedTransaction> txs = await store.GetByBlockNumberAsync(0);
List<Receipt> receipts = await ((IReceiptStore)store).GetByBlockNumberAsync(0);   // bloom/logs re-derived on read
```

The store is read-only: every interface's `SaveAsync` throws (the freezer is written only by
`FreezerPromotionService`). A delete or block-hash update below the freeze boundary throws
`FreezerImmutableException` (frozen history is immutable); at or above it, where the hot window still
owns the block, it is a no-op.

### Decode-once cache

Body decode plus per-tx sender recovery (ECDSA) is the expensive part of reading a block, so it is
cached once per block. `DecodedClusterCache` is a bounded, thread-safe, decode-once LRU:

```csharp
public sealed class DecodedClusterCache
{
    public DecodedClusterCache(int maxBlocks);
    public int Count { get; }
    public DecodedCluster GetOrAdd(long blockNumber, Func<long, DecodedCluster> factory);
    public void Invalidate(long blockNumber);
}

public sealed class DecodedCluster
{
    public BlockBodyCluster Body { get; }
    public IReadOnlyList<DerivedReceipt> Receipts { get; }
    public IReadOnlyList<string> Senders { get; }             // recovered sender per tx
    public IReadOnlyList<string> ContractAddresses { get; }   // deployment address per create-tx, else null
}
```

Concurrent readers of the same block wait on one decode and share its result; readers of different
blocks never block each other.

## FilterMaps log index (EIP-7745)

The `Nethereum.CoreChain.Freezer.FilterMaps` namespace renders finalized blocks into geth's `fm-`
filtermaps log index and answers `eth_getLogs` over it, driving the pure, storage-agnostic filtermaps
core — `LogValueHasher`, `FilterMapsParams`, `FilterMapsSchema`, `FilterMapsRowCodec`,
`FilterMapsMatcher`, `IFilterMapsStore` — documented in the `Nethereum.Freezer` package. In production
the rendered rows live in RocksDB (`Nethereum.CoreChain.RocksDB`); this package supplies the render
coordinator, the query engine, and the freezer-backed chain/finality views they run against.

### Chain and finality views over the freezer

The render and query stack reads a live fork through `IChainView` and measures finality through
`IFinalitySource`. `FreezerChainView` and `FreezerHeadFinalitySource` are the production implementations
over the freezer.

```csharp
public interface IChainView
{
    long HeadNumber { get; }
    byte[] BlockId(long number);
    BlockHeader Header(long number);
    IReadOnlyList<ReceiptForStorage> Receipts(long number);
}

public sealed class FreezerChainView : IChainView
{
    public FreezerChainView(IFrozenReadSource freezer, FreezerCodecSet codecs);
}

public sealed class FreezerHeadFinalitySource : IFinalitySource
{
    public FreezerHeadFinalitySource(IFrozenReadSource freezer);
}
```

### Rendering — `FilterMapsIndexer`

`FilterMapsIndexer` renders finalized, promoted-but-unindexed blocks into the `fm-` index one whole
epoch at a time, and un-renders on a finality regression. It writes nothing until an epoch is fully
computed in memory, so a crash mid-render simply re-renders from the last persisted range — idempotent,
because the same receipts always produce the same rows.

```csharp
public sealed class FilterMapsIndexer
{
    public FilterMapsIndexer(IFilterMapsStore store, IChainView chain, IFinalitySource finality, FilterMapsParams p,
        int renderDegreeOfParallelism = 1);

    public FilterMapsParams Params { get; }
    public long IndexedHeadBlock { get; }               // last indexed block; -1 when empty
    public bool RenderHead();                            // render one epoch; false if not enough finalized data
    public int RenderChunk(                              // render every complete epoch spanned by one decoded receipts chunk
        IReadOnlyList<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)> chunk, long finalizedBlockBound);
    public bool RollbackTo(long newHead);               // un-render on finality regression
}
```

```csharp
var indexer = new FilterMapsIndexer(store, new FreezerChainView(freezer, codecs),
    new FreezerHeadFinalitySource(freezer), FilterMapsParams.Default);

while (indexer.RenderHead()) { }                         // render every complete epoch available
long indexedHead = indexer.IndexedHeadBlock;
```

### Querying — `FilterMapsQueryEngine`

`FilterMapsQueryEngine` is the `eth_getLogs` façade. It snapshots the indexed-head boundary once, serves
`[fromBlock, indexedHead]` from the filtermaps index and `(indexedHead, toBlock]` from a hot/scan path,
and merges the two in block/tx/log order — no gap, no double-count.

```csharp
public sealed class FilterMapsQueryEngine
{
    public FilterMapsQueryEngine(
        IFilterMapsStore store,
        FilterMapsMatcher matcher,
        FilterMapsLogResolver resolver,
        IHistoricalLogScan hotScan);

    public Task<IReadOnlyList<ResolvedLog>> GetLogsAsync(LogFilter filter);
}
```

```csharp
var engine = new FilterMapsQueryEngine(
    store,
    new FilterMapsMatcher(new FilterMapsQueryBackend(store, FilterMapsParams.Default)),
    new FilterMapsLogResolver(store, chain, FilterMapsParams.Default),
    hotScan);

var filter = new LogFilter { Addresses = new List<string> { contractAddress }, FromBlock = 0, ToBlock = indexedHead };
IReadOnlyList<ResolvedLog> logs = await engine.GetLogsAsync(filter);
```

The index yields *candidates*, not verified matches. `FilterMapsLogResolver` resolves each candidate lv
index to the real log it names — or `null` for a false positive (a topic slot, a delimiter, or past the
block's content) — and the engine re-checks the resolved log's own address/topics against the filter.

```csharp
public sealed class FilterMapsLogResolver
{
    public FilterMapsLogResolver(IFilterMapsStore store, IChainView chain, FilterMapsParams p);
    public ResolvedLog GetLogByLvIndex(long lvIndex);
}

public sealed class ResolvedLog
{
    public long BlockNumber { get; }
    public int TransactionIndex { get; }
    public int LogIndex { get; }                        // block-cumulative
    public Log Log { get; }
}
```

`IHistoricalLogScan` is the hot/scan seam for the `(indexedHead, toBlock]` band:

```csharp
public interface IHistoricalLogScan
{
    Task<IReadOnlyList<ResolvedLog>> ScanAsync(LogFilter filter, long fromBlock, long toBlock);
}
```

### The rendering primitives

`FilterMapsIndexer` builds on two lower-level pieces, reusable directly:

- **`LogValueSequence`** — the pure per-block, per-log log-value sequencer. Each log contributes
  `1 + topics.Count` sequential lv slots (address first, then one per topic); a delimiter slot separates
  consecutive blocks; a log's value group never splits across a map boundary.

  ```csharp
  public static class LogValueSequence
  {
      public static IEnumerable<LogValueEntry> Enumerate(
          long fromBlock, long toBlock, long startLvIndex, long? precedingBlock,
          IChainView chain, FilterMapsParams p, bool deferValueHashing = false);
  }

  public readonly struct LogValueEntry
  {
      public long BlockNumber { get; }
      public long LvIndex { get; }
      public byte[] ValueHash { get; }
      public bool IsBlockDelimiter { get; }

      public static LogValueEntry Value(long blockNumber, long lvIndex, byte[] valueHash);
      public static LogValueEntry Delimiter(long blockNumber, long lvIndex);
  }
  ```

- **`FilterMapRenderer`** — turns a stream of log values into `(map, row)` marks in memory, escalating a
  colliding row to the next layer before placing a mark.

  ```csharp
  public sealed class FilterMapRenderer
  {
      public FilterMapRenderer(FilterMapsParams p);
      public void Mark(long mapIndex, long lvIndex, byte[] valueHash);
      public IEnumerable<long> TouchedMapIndices { get; }
      public IReadOnlyDictionary<int, List<uint>> RowsOfMap(long mapIndex);
  }
  ```
