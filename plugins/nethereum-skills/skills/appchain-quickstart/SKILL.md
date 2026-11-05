---
name: appchain-quickstart
description: Help users launch and configure Nethereum AppChains — domain-specific Ethereum extension layers with full EVM, sequencer, follower sync, RocksDB storage. Use this skill whenever the user mentions AppChain, application chain, custom chain, launching a sequencer, syncing follower nodes, RocksDB blockchain storage, AppChainBuilder, or building a domain-specific chain with .NET/C#.
user-invocable: true
---

# AppChain Quickstart

> **PREVIEW** — AppChain packages are in preview. APIs may change between releases.

A Nethereum AppChain is a lightweight, domain-specific extension layer for Ethereum L1/L2. It runs a full EVM, exposes standard JSON-RPC, and supports follower sync with state verification.

## When to Use This Skill

- User wants to launch a custom blockchain/chain with .NET
- User needs a sequencer + follower architecture
- User wants persistent blockchain storage with RocksDB
- User wants to sync and verify chain state across nodes
- User mentions AppChain, application chain, or domain-specific chain

## Packages

```bash
# CLI tool (sequencer/follower server)
dotnet tool install --global Nethereum.AppChain.Server

# Programmatic usage
dotnet add package Nethereum.AppChain.Sequencer   # AppChainBuilder
dotnet add package Nethereum.CoreChain             # Storage interfaces + in-memory
dotnet add package Nethereum.CoreChain.RocksDB     # RocksDB persistent storage
dotnet add package Nethereum.AppChain.Server.Core   # host a node in process (AppChainServerRunner, AppChainServerConfig); followers sync over Nethereum.DevP2P.Sync
```

## Quick Launch (CLI)

### Sequencer (produces blocks)

```bash
nethereum-appchain \
  --port 8546 \
  --chain-id 420420 \
  --name "MyAppChain" \
  --genesis-owner-key 0xYOUR_PRIVATE_KEY \
  --sequencer-key 0xYOUR_PRIVATE_KEY \
  --block-time 1000 \
  --enable-devp2p-serve --devp2p-serve-port 30403
```

### Follower (syncs and verifies)

```bash
nethereum-appchain \
  --port 8547 \
  --chain-id 420420 \
  --genesis-owner-address 0xOWNER_ADDRESS \
  --sequencer-address 0xSEQUENCER_ADDRESS \
  --follow-peer enode://<sequencer-node-id>@<host>:30403
```

The follower dials the sequencer over DevP2P, snap-bootstraps when the sequencer is past the pivot trail distance, re-executes every block and rejects a state-root mismatch; it accepts only blocks signed by the key behind `--sequencer-address`.

## Programmatic AppChain (AppChainBuilder)

For embedded usage, testing, or when you need programmatic control:

```csharp
using Nethereum.AppChain.Sequencer.Builder;

// Simple in-memory chain
var chain = await new AppChainBuilder("TestChain", 420420)
    .WithOperator(privateKey)
    .BuildAsync();

// With custom config (in-memory; for RocksDB run the CLI or wire RocksDb* stores into new AppChain(...))
var chain = await new AppChainBuilder("MyChain", 420420)
    .WithOperator(privateKey)
    .WithBaseFee(0)
    .WithBlockGasLimit(30_000_000)
    .WithOnDemandBlocks()       // Block only when txns arrive
    .WithPrefundedAddresses(new[] { addr1, addr2 })
    .BuildAsync();
```

### Presets

```csharp
// Gaming — optimized for high-frequency state updates
var chain = await AppChainPresets
    .ForGaming("GameChain", 420420, operatorKey)
    .BuildAsync();

// Testing — in-memory, on-demand blocks
var chain = await AppChainPresets
    .ForTesting("TestChain", 31337, testKey)
    .BuildAsync();
```

### In-Process RPC (No HTTP)

```csharp
var node = new AppChainNode(chain.AppChain, chain.Sequencer);
var rpcClient = new AppChainRpcClient(node, chainId: 420420);
var web3 = new Web3(new Account(userKey, 420420), rpcClient);

// Use web3 normally — no HTTP overhead
var balance = await web3.Eth.GetBalance.SendRequestAsync(userAddress);
```

## Interact via Nethereum

Once running, connect with standard Nethereum:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account(privateKey, chainId: 420420);
var web3 = new Web3(account, "http://localhost:8546");

// Genesis owner is pre-funded
var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);

// Deploy and interact with contracts — same as any Ethereum network
var receipt = await web3.Eth.GetContractDeploymentHandler<MyContractDeployment>()
    .SendRequestAndWaitForReceiptAsync(new MyContractDeployment { ... });
```

## Storage

### In-Memory (development)

```csharp
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;

var blockStore = new InMemoryBlockStore();
var txStore = new InMemoryTransactionStore(blockStore);
var receiptStore = new InMemoryReceiptStore();
var logStore = new InMemoryLogStore();
var stateStore = new InMemoryStateStore();
var trieNodeStore = new InMemoryContentNodeStore();
```

### RocksDB (production)

```csharp
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;

var options = new RocksDbStorageOptions { DatabasePath = "./chaindata" };
using var manager = new RocksDbManager(options);

var blockStore = new RocksDbBlockStore(manager);
var txStore = new RocksDbTransactionStore(manager, blockStore);
var receiptStore = new RocksDbReceiptStore(manager, blockStore);
var logStore = new RocksDbLogStore(manager);
var stateStore = new RocksDbStateStore(manager);
var trieNodeStore = new RocksDbTrieNodeStore(manager);
```

### DI Registration

```csharp
services.AddRocksDbStorage("./chaindata");
```

## Sync

### DevP2P Follower

A follower uses the shared DevP2P stack (`Nethereum.DevP2P.Sync`), composed for AppChain by `AppChainDevP2PFollower` (`Nethereum.AppChain.Server.Core`). Launch it with the CLI above (`--follow-peer`, `--genesis-owner-address`, `--sequencer-address`); the sequencer must run with `--enable-devp2p-serve`. In process, build an `AppChainServerConfig` with `Genesis.Owner.Address`, `Consensus.Sequencer.Address`, `Node.Sync.Mode = SyncMode.ForwardExecute` and `Node.Sync.FollowPeerEnode`, then `await AppChainServerRunner.RunAsync(config, cancellationToken)`.

### Finality

```csharp
bool isFinal = await finalityTracker.IsFinalizedAsync(blockNumber);  // L1-anchored
bool isSoft = await finalityTracker.IsSoftAsync(blockNumber);         // Synced, not anchored
```

## Key Decisions

| Decision | Guidance |
|----------|----------|
| CLI vs AppChainBuilder | CLI for production servers; AppChainBuilder for embedded/testing |
| In-Memory vs RocksDB | In-memory for tests/demos; RocksDB for anything that needs persistence |
| Block time | 1000ms default; 100ms for testing; `WithOnDemandBlocks()` for test suites |
| State re-execution | Followers verify every state root by default; leave `Node.Sync.ContinueOnMismatch` at `false` (it exists for debugging) |

## Common Gotchas

- **Same genesis = same chain** — sequencer and followers must use the same genesis owner address, sequencer address and chain ID; a follower refuses blocks not signed by its configured sequencer address
- **RocksDB is the default** — CLI uses RocksDB unless you pass `--in-memory`
- **One RocksDbManager per path** — don't create multiple managers pointing at the same directory
- **Port 8546** — AppChain defaults to 8546; use `--port` to change it
- **RPC exposure** — the port serves `eth`, `net`, `web3` only; `admin`, `debug`, `txpool` need `--rpc-modules`; a foreign `Host` header is answered 403 (`--rpc-vhosts`); browser WebSocket origins need `--ws-origins`

For full documentation, see: https://docs.nethereum.com/docs/application-chain/overview
