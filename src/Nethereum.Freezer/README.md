# Nethereum.Freezer

A geth-compatible append-only **ancient store** ("freezer") for write-once, sequential-by-block historical
chain data: headers, block hashes, bodies, receipts, and EIP-7928 block access lists (bals).

## Overview

Historical block data is written once and read rarely, so it belongs in flat append-only files (~1× write
amplification, no compaction) rather than a leveled-compaction LSM (10–30× write amplification). This package
reads and writes geth's on-disk freezer format, so a geth `ancient/` directory can be imported directly and the
format interoperates with the wider portable-history tooling.

The archive is **not** five independent tables — it is one lockstep unit. Every finalized block writes exactly
one item to every table (headers, hashes, bodies, receipts, bals), from genesis. Reads are by absolute block
number; the coordinator translates that to each table's physical item and applies the table's codec.

Pure and dependency-light — no RocksDB or CoreChain dependency, so transports, tooling, and AppChains reuse it.
The CoreChain integration (finality-gated promotion, receipt-field derivation, the composite-store adapter, the
`fm-` filtermaps log index driven by RocksDB) lives alongside in the CoreChain assemblies; this package is the
byte-level store plus the pure filtermaps hashing/codec/matcher core.

## Geth format & interoperability

The "freezer" ancient store was introduced by **go-ethereum (geth)** and is the de-facto standard for archival
block data. This package implements that same on-disk format: the fixed multi-table layout (headers, hashes,
bodies, receipts, bals), snappy-compressed `.cdat`/`.cidx` rolling data/index files (and the raw `.rdat`/`.ridx`
pair for the uncompressed `hashes` table), and lockstep multi-table append where every block writes one item to
every table.

Nethereum interoperates with an existing geth freezer directory: `GethFreezerImporter.InspectSource` opens a
copied geth `ancient/` directory read-only and returns an `ImportReadiness` reporting whether any tail-group was
pruned by geth's history-expiry (each pruned group surfaced as a `PrunedGroup`).

## On-disk format

Each table is stored as a small index file plus one or more rolled data files, following geth's
`freezer_table.go` layout:

| Artifact | Purpose |
| --- | --- |
| `<name>.cidx` / `<name>.ridx` | Index file: `count + 1` fixed **6-byte** entries (entry 0 is a sentinel). `.cidx` for compressed tables, `.ridx` for the raw table. |
| `<name>.NNNN.cdat` / `<name>.NNNN.rdat` | Data file(s). Items are appended back-to-back; a file is *rolled* (never split) once appending would exceed the size cap. |
| `<name>.meta` | 3-field RLP: version, `virtualTail` (items logically pruned), `flushOffset` (durably-fsynced index length in bytes). |

Each index entry is `FreezerIndexEntry` — big-endian `filenum` (uint16) then `offset` (uint32), 6 bytes total
(`FreezerIndexEntry.Size`). An item's byte span is the gap between consecutive entries.

The fixed 5-table chain-freezer layout (`FreezerLayout`), matching geth's `ancient_scheme.go`:

| Table | Compression | Index / data extension | Tail group |
| --- | --- | --- | --- |
| `headers` | snappy | `.cidx` / `.cdat` | HeadersAndHashes |
| `hashes` | none (raw) | `.ridx` / `.rdat` | HeadersAndHashes |
| `bodies` | snappy | `.cidx` / `.cdat` | BlockData |
| `receipts` | snappy | `.cidx` / `.cdat` | BlockData |
| `bals` | snappy | `.cidx` / `.cdat` | Bal |

`FreezerTableConfig` (`Name`, `UseCompression`, `TailGroup`) describes one table; `FreezerTailGroup` groups the
tables that must never drift apart when pruned (`HeadersAndHashes`, `BlockData`, `Bal`).

## Installation

```bash
dotnet add package Nethereum.Freezer
```

### Dependencies

Nethereum.RLP, Nethereum.Documentation (direct project references); IronSnappy (package). Targets
net8.0/net9.0/net10.0.

## Open the store — `Freezer.Open`

`Freezer` is the coordinator over the five lockstep tables. Open a directory with a `FreezerLayout` and a
`FreezerOpenMode`:

```csharp
public static Freezer Open(FreezerLayout layout, FreezerOpenMode mode)
```

`FreezerOpenMode` is `Append` (for writing/sync — runs the self-healing repair pass) or `ReadOnly` (for
import/serving — validates and **never mutates**, failing loudly on a torn or short copy). Construct a layout
with a directory (and optionally a max data-file size):

```csharp
var layout = new FreezerLayout(ancientDirectory);              // maxFileSize defaults to RollingDataFiles.DefaultMaxFileSize (2 GiB - 1)
using var freezer = Freezer.Open(layout, FreezerOpenMode.ReadOnly);

long itemCount = freezer.Items;                                // logical archive item count (min head across non-empty tables)
```

`Freezer.Items` is the archive's logical item count. `Freezer.Tails` (`IReadOnlyDictionary<string, TableTail>`)
reports each tail-group's reconciled `VirtualTail` (oldest retained item) and its member table names. `Freezer`
is `IDisposable`.

## Read — cluster and narrow accessors

`ReadCluster` returns all five tables' items for a block as a `FrozenBlockCluster` (each item already
decompressed):

```csharp
public FrozenBlockCluster ReadCluster(long blockNumber)
```

`FrozenBlockCluster` exposes `Header`, `Hash`, `Body`, `Receipts`, `Bal` as `byte[]` (never null; pre-EIP-7928
`Bal` is the empty-BAL placeholder). Because `ReadCluster` touches all five tables' data files, there are narrow
single-table accessors for a caller that only needs a subset:

```csharp
public byte[] ReadHeader(long blockNumber)
public byte[] ReadHash(long blockNumber)
public byte[] ReadBody(long blockNumber)
public byte[] ReadReceipts(long blockNumber)
```

```csharp
var cluster = freezer.ReadCluster(0);
byte[] headerRlp = cluster.Header;

byte[] justTheHash = freezer.ReadHash(0);                      // reads only the hashes table
```

### Concurrent-read boundaries

Two boundaries tell a concurrent reader how far it may safely read while an appender is still running. Both take
the specific table names the reader actually reads (naming all five would let an unrelated slow-rolling table
starve the boundary) and return the **min** across them:

```csharp
public long SealedHead(params string[] tableNames)
public long CommittedHead(params string[] tableNames)
```

`SealedHead` is the item count sealed in data files the named tables will never append to again (never touches
the file the appender is extending). `CommittedHead` is the min durably-fsynced head, derived from each index's
flush offset — so a small never-rolled table no longer pins the boundary at zero. Both throw `ArgumentException`
if no table name is given.

```csharp
long safeToIndex = freezer.CommittedHead("hashes", "bodies", "receipts");
```

## Write — batch, append, commit

Writing goes through a `FreezerBatch`. One block is one cluster written to every table; a batch spans many
clusters and `Commit` is the single durability (fsync) boundary:

```csharp
public FreezerBatch BeginBatch()
```

```csharp
public void AppendCluster(long blockNumber, FrozenBlockCluster cluster)
public void AppendEncodedCluster(long blockNumber, EncodedFrozenCluster cluster)
public long Commit()
public void Reset()
```

`AppendCluster` enforces lockstep: every table must sit exactly at `blockNumber` before any table is touched,
or it throws `FreezerConsistencyException`. `Commit` fsyncs every index, then every data file, then persists
every `.meta` (the commit point), and returns the new `Freezer.Items`. `Reset` discards staged-but-uncommitted
appends, physically truncating back to the batch's last commit.

```csharp
var layout = new FreezerLayout(ancientDirectory);
using var freezer = Freezer.Open(layout, FreezerOpenMode.Append);

using (var batch = freezer.BeginBatch())
{
    for (long block = 0; block < 5; block++)
    {
        var cluster = new FrozenBlockCluster(
            header:   headerRlp[block],
            hash:     hash[block],       // 32 bytes
            body:     bodyRlp[block],
            receipts: receiptsRlp[block],
            bal:      balRlp[block]);    // empty-BAL placeholder pre-EIP-7928
        batch.AppendCluster(block, cluster);
    }

    long committedHead = batch.Commit();   // == 5
}
```

### Off-thread encoding

Compression (snappy) is the CPU-bound half of a freeze. `Freezer.Encode` runs every table's codec over a cluster
off the append thread; the result is fed to `AppendEncodedCluster`, so the append thread only writes bytes:

```csharp
public EncodedFrozenCluster Encode(FrozenBlockCluster cluster)
```

`EncodedFrozenCluster` mirrors `FrozenBlockCluster`'s five `byte[]` members, already codec-encoded.

## Truncate (deep reorg)

`TruncateHead` rolls the whole archive back to `newItemCount` — the coordinator-guarded path for a
finality-regression / deep-reorg:

```csharp
public void TruncateHead(long newItemCount)
```

It rejects (`FreezerImmutableException`) any target below the highest `VirtualTail` across tables — truncating
into already-pruned, immutable history — and is a no-op for a target above the current head.

## Import a copied geth freezer — `GethFreezerImporter`

Copying a geth `ancient/` directory only equals "complete history" if geth's history-expiry pruning never ran
against the source. `GethFreezerImporter` opens the copy read-only and reports whether any tail-group was pruned:

```csharp
public ImportReadiness InspectSource(FreezerLayout source)
public static ImportReadiness BuildReadiness(IReadOnlyDictionary<string, TableTail> tails, long items)
```

`ImportReadiness` exposes `IsCompleteHistory` (true when nothing is pruned), `PrunedGroups`
(`IReadOnlyList<PrunedGroup>`), and `Items`. Each `PrunedGroup` names the tail-group (`GroupLabel`), its
`VirtualTail`, and its `Members` — a non-zero tail is an Era1-pruned gap this store cannot read.

```csharp
var readiness = new GethFreezerImporter().InspectSource(new FreezerLayout(copiedAncientDir));
if (!readiness.IsCompleteHistory)
    foreach (var pruned in readiness.PrunedGroups)
        Console.WriteLine($"pruned {pruned.GroupLabel} up to {pruned.VirtualTail}");
```

## Codecs

The store is byte-level; a codec maps one freezer item to/from `T`:

```csharp
public interface IItemCodec<T>
{
    byte[] Encode(T item);
    T Decode(ReadOnlySpan<byte> bytes);
}
```

- **`IdentityByteCodec`** (`IItemCodec<byte[]>`) — the raw leaf codec; the `hashes` table stores bytes with no
  framing.
- **`SnappyItemCodec<T>`** — a decorator that applies geth's snappy block format around an inner codec; every
  compressed table wraps `IdentityByteCodec` in one of these.

## Lower-level primitives

For direct table access (the coordinator builds on these):

- **`FreezerTable<T>`** — one stream (index + rolled data files + codec). `OpenReadOnly` / `OpenForAppend`
  factories; `Read`, `Append`, `Encode`/`AppendEncoded`, `SyncIndex`/`SyncData`/`PersistMeta`, `TruncateHead`;
  properties `Name`, `Count`, `VirtualTail`, `DurableHead`, `SealedHead`.
- **`FreezerTablePaths`** — resolves `IndexPath` / `MetaPath` from directory + name + compression flag.
- **`FreezerTableMeta`** — the `.meta` RLP (`Version`, `VirtualTail`, `FlushOffset`); `Encode` / `Decode` /
  `WriteAtomic` (temp + fsync + rename). `SupportedVersion` is 2.
- **`FreezerIndex`** (`IFreezerIndex`) — the direct-seek 6-byte-entry index file; positional reads via
  `RandomAccess`.
- **`RollingDataFiles`** (`IFreezerDataFiles`) — the rolled `.cdat`/`.rdat` data files; `DefaultMaxFileSize` is
  `2 GiB − 1`.
- **`FreezerIndexEntry`** / **`ItemRange`** / **`TableTail`** — the index-record, resolved `(file, start,
  length)` span, and reconciled tail-group value.

## Exceptions

- **`FreezerConsistencyException`** — a coordinator lockstep invariant was broken (an upstream bug).
- **`FreezerImmutableException`** — a mutation at or below the tail/freeze boundary was attempted.
- **`FreezerValidationException`** — read-only `Validate` found a defect append-mode `Repair` would have healed.
- **`FreezerFormatException`** — an on-disk artifact (`.meta`, index) is malformed or an unsupported version.

## FilterMaps log index (pure core)

The package also carries the pure, storage-agnostic core of geth's `filtermaps` (EIP-7745-style) log index —
the hashing, byte layout, and matcher — under the `Nethereum.Freezer.FilterMaps` namespace. The rendered rows
live in an LSM in production (`Nethereum.CoreChain.RocksDB`), not the freezer; this package supplies the format
and the query logic.

- **`FilterMapsParams`** — the filter-map geometry (`FilterMapsParams.Default`, `MapHeight`, `MaxRowLength`,
  `Sanitize`, …).
- **`LogValueHasher`** — the pure log-value → (row, column) hashing (`AddressValue`, `TopicValue`, `RowIndex`,
  `ColumnIndex`, `MaskedMapIndex`).
- **`FilterMapsSchema`** — the `fm-` KV key builders (`RangeKey`, `BaseRowKey`, `ExtRowKey`,
  `LastBlockOfMapKey`, `BlockLvPointerKey`, `MapRowIndex`).
- **`FilterMapsRowCodec`** / **`FilterRow`** — encode/decode base-row groups and ext rows; a `FilterRow` is a
  list of `uint` column indices (`FilterRow.Empty`).
- **`FilterMapsRange`** — the `fm-R` range value (8-field RLP mirroring geth's struct).
- **`IFilterMapsStore`** / **`InMemoryFilterMapsStore`** — the KV port over the `fm-` layout and its dict-backed
  test fake.
- **`IFilterMapsQueryBackend`** / **`FilterMapsQueryBackend`** — the read port the matcher needs and its adapter
  over `IFilterMapsStore` + `FilterMapsRowCodec`.
- **`FilterMapsQuery`** / **`FilterMapsMatcher`** — a filter expressed as ordered lv-index positions, and the
  matcher that ANDs them: `GetPotentialMatches(FilterMapsQuery query, long fromBlock, long toBlock)`.
