# Nethereum.AppChain.Sync

> **PREVIEW** — This package is in preview. APIs may change between releases.

Synchronisation services for [Nethereum AppChain](../Nethereum.AppChain/README.md) follower nodes — enabling anyone to independently sync, verify, and read AppChain state.

## Overview

A core promise of the AppChain model is that state is synchronisable by anyone. This package delivers on that promise: follower nodes catch up with the sequencer and maintain a verified copy of the chain state, with every block re-executed and every state root validated.

It implements a two-phase sync strategy: batch-based historical import from L1-anchored checkpoints followed by live block polling from peers. The package includes multi-peer management with automatic failover, state reconstruction through block re-execution, finality tracking (soft vs L1-finalized blocks), and coordinated sync that transitions seamlessly between batch and live phases.

### Key Features

- **Two-Phase Sync**: Batch import for historical data, live polling for chain head
- **Multi-Peer Failover**: Health-checked peer pool with automatic best-peer selection
- **State Reconstruction**: Optional block re-execution to build and validate state
- **Finality Tracking**: Distinguishes soft (peer-synced) from finalized (L1-anchored) blocks
- **Coordinated Sync**: Orchestrates batch → live transitions automatically
- **Batch Management**: File-based batch storage with compression and verification

## Installation

```bash
dotnet add package Nethereum.AppChain.Sync
```

### Dependencies

- **Nethereum.CoreChain** - `TransactionProcessor`, `IncrementalStateRootCalculator`, storage interfaces
- **Nethereum.AppChain** - Core chain abstraction
- **Nethereum.Model** - Block headers, transactions, receipts
- **Nethereum.Util** - Keccak hashing and byte utilities
- **Microsoft.Extensions.Logging.Abstractions** - Structured logging

## Key Concepts

### Two-Phase Synchronization

Phase 1 (**Batch Sync**): Import pre-packaged block batches from anchored history. Batches are downloaded from the sequencer or mirror URLs, verified against their hash, and imported into local storage. This provides fast, finalized catch-up.

Phase 2 (**Live Sync**): Poll peers for new blocks as they are produced. `MultiPeerSyncService` follows the chain head with configurable poll intervals and automatic peer switching on failures.

### Finality Tiers

Blocks have two finality levels:
- **Soft**: Recently synced from peers, may be subject to reorgs
- **Finalized**: Anchored to L1, cryptographically immutable

The `IFinalityTracker` manages these states, enabling dApps to choose their safety guarantees.

### Block Re-Execution

`BlockReExecutor` optionally re-executes transactions during sync to reconstruct state locally. This validates that block state roots match headers and allows followers to serve state queries:

```csharp
var reExecutor = new BlockReExecutor(
    transactionProcessor, stateStore, chainConfig, stateRootCalculator);
```

### Peer Management

`PeerManager` maintains a health-checked pool of sync peers with automatic selection of the best peer by block height and latency:

```csharp
var peerManager = new PeerManager(config, clientFactory);
peerManager.AddPeer("http://sequencer:8546");
await peerManager.StartHealthCheckAsync();
var bestClient = await peerManager.GetHealthyClientAsync();
```

## Quick Start

```csharp
using Nethereum.AppChain.Sync;

var peerManager = new PeerManager(new PeerManagerConfig());
peerManager.AddPeer("http://sequencer:8546");

var syncConfig = new MultiPeerSyncConfig
{
    PollIntervalMs = 100,
    AutoFollow = true,
    RejectOnStateRootMismatch = true
};

var syncService = new MultiPeerSyncService(syncConfig,
    blockStore, txStore, receiptStore, logStore,
    finalityTracker, peerManager, blockReExecutor);

await syncService.StartAsync();
```

## Usage Examples

### Example 1: Live Block Sync with State Validation

```csharp
using Nethereum.AppChain.Sync;

var syncService = new MultiPeerSyncService(
    new MultiPeerSyncConfig { PollIntervalMs = 100, AutoFollow = true },
    blockStore, txStore, receiptStore, logStore,
    finalityTracker, peerManager, blockReExecutor);

syncService.BlockImported += (sender, args) =>
{
    Console.WriteLine($"Block {args.Header.BlockNumber} imported");
};

syncService.Error += (sender, args) =>
{
    Console.WriteLine($"Sync error: {args.Message}");
};

await syncService.StartAsync();
```

### Example 2: Coordinated Two-Phase Sync

```csharp
using Nethereum.AppChain.Sync;

var coordinated = new CoordinatedSyncService(
    new CoordinatedSyncConfig { AutoStart = true },
    batchSyncService, liveSyncService,
    finalityTracker, anchorService, batchStore);

coordinated.SyncProgressChanged += (sender, args) =>
{
    Console.WriteLine($"Phase: {args.Phase}, Block: {args.BlockNumber}");
};

coordinated.BatchFinalized += (sender, args) =>
{
    Console.WriteLine($"Batch finalized up to block {args.ToBlock}");
};

await coordinated.StartAsync();
```

### Example 3: Batch Import

```csharp
using Nethereum.AppChain.Sync;

var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore);
var result = await importer.ImportBatchFromFileAsync(
    filePath: "chain-420420-blocks-0-100.gz",
    expectedHash: batchHash,
    verificationMode: BatchVerificationMode.Full,
    compressed: true);

Console.WriteLine($"Imported {result.BlockCount} blocks, {result.TransactionCount} transactions");
```

### Example 4: Peer Management

```csharp
using Nethereum.AppChain.Sync;

var peerManager = new PeerManager(new PeerManagerConfig
{
    HealthCheckIntervalMs = 5000,
    HealthCheckTimeoutMs = 3000,
    MaxFailuresBeforeRemoval = 5
});

peerManager.AddPeer("http://node1:8546");
peerManager.AddPeer("http://node2:8546");
peerManager.AddPeer("http://node3:8546");

peerManager.PeerStatusChanged += (sender, args) =>
{
    Console.WriteLine($"Peer {args.Url}: healthy={args.IsHealthy}");
};

await peerManager.StartHealthCheckAsync();
var best = peerManager.GetBestPeer();
```

## API Reference

### MultiPeerSyncService

Real-time block following with multi-peer failover.

```csharp
public class MultiPeerSyncService : ILiveBlockSync
{
    public long LocalTip { get; }
    public long RemoteTip { get; }
    public LiveSyncState State { get; }

    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();
    public Task SyncToLatestAsync();
    public Task SyncToBlockAsync(long blockNumber);

    public event EventHandler<BlockImportedEventArgs>? BlockImported;
    public event EventHandler<SyncErrorEventArgs>? Error;
}
```

### CoordinatedSyncService

Two-phase sync orchestration (batch then live).

```csharp
public class CoordinatedSyncService
{
    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();

    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;
    public event EventHandler<BatchFinalizedEventArgs>? BatchFinalized;
}
```

### PeerManager

Peer pool with health checking and best-peer selection.

```csharp
public class PeerManager : IPeerManager
{
    public void AddPeer(string url);
    public void RemovePeer(string url);
    public SyncPeer? GetBestPeer();
    public Task<ISequencerRpcClient?> GetHealthyClientAsync();
    public Task StartHealthCheckAsync(CancellationToken ct = default);
}
```

### IFinalityTracker

Block finality state management.

```csharp
public interface IFinalityTracker
{
    Task<bool> IsFinalizedAsync(long blockNumber);
    Task<bool> IsSoftAsync(long blockNumber);
    Task MarkAsFinalizedAsync(long blockNumber);
    Task MarkRangeAsFinalizedAsync(long fromBlock, long toBlock);
    Task<long> GetLatestFinalizedBlockAsync();
}
```

### BatchInfo

Batch metadata structure.

Key properties:
- `FromBlock` / `ToBlock` - Block range
- `BatchHash` - Content hash for verification
- `Status` - Pending, Created, Written, Anchored, Verified, Imported, Failed
- `ToBlockStateRoot` / `ToBlockTxRoot` / `ToBlockReceiptRoot` - Root hashes

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - HTTP server with sync endpoints
- **[Nethereum.AppChain.Sequencer](../Nethereum.AppChain.Sequencer/README.md)** - Sequencer coordinator uses sync for initial catch-up

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Transaction processor and state root calculation
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
