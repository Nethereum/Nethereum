# Nethereum.AppChain.Sequencer

> **PREVIEW** — This package is in preview. APIs may change between releases.

Transaction ordering, block production, policy enforcement, and batch creation for [Nethereum AppChain](../Nethereum.AppChain/README.md) networks.

## Overview

The sequencer is the centralised operator in an AppChain — your business, your rules. It accepts transactions, validates them against configurable policies, orders them into blocks, and produces blocks at configurable intervals or on demand. All produced state is publicly verifiable and synchronisable by any follower.

The sequencer integrates with pluggable consensus strategies (single-sequencer or Clique PoA), supports batch production for L1 anchoring, and provides a coordinator for managing sequencer lifecycle during initial sync and production phases.

### Key Features

- **Transaction Validation Pipeline**: Nonce checking, balance validation, intrinsic gas calculation, and sender recovery
- **Configurable Block Production**: Interval-based or on-demand block production modes
- **Policy Enforcement**: Pluggable access control with sender allowlists and calldata size limits
- **Batch Production**: Aggregates blocks into batches for L1 anchoring with optional compression
- **Sequencer Coordinator**: Manages sync-to-production lifecycle transitions
- **Circuit Breaker**: Stops block production after consecutive failures

## Installation

```bash
dotnet add package Nethereum.AppChain.Sequencer
```

### Dependencies

- **Nethereum.CoreChain** - `BlockProducer`, `TransactionProcessor`, `ITxPool`, `IBlockProductionStrategy`
- **Nethereum.AppChain** - `IAppChain` and `AppChainConfig`
- **Nethereum.AppChain.Sync** - `IBatchStore` for batch metadata tracking
- **Nethereum.Signer** - Transaction signature verification and sender recovery
- **Nethereum.Model** - Transaction, block, and receipt data structures

## Key Concepts

### Block Production Modes

The sequencer supports two production modes:

- **Interval-based** (default): Produces blocks at a fixed interval (e.g., every 1000ms), collecting pending transactions from the pool
- **On-demand**: Produces a block immediately when a transaction is submitted, providing instant confirmation

```csharp
var intervalConfig = SequencerConfig.Default;       // 1000ms interval
var onDemandConfig = SequencerConfig.OnDemand;      // Immediate production
```

### Transaction Validation Pipeline

Every submitted transaction passes through validation before entering the pool:

1. **Policy enforcement** - Check sender authorization and calldata limits
2. **Sender recovery** - Recover sender address from ECDSA signature
3. **Nonce checking** - Verify nonce matches expected next nonce
4. **Balance validation** - Ensure sender has sufficient funds for value + gas
5. **Intrinsic gas calculation** - Verify gas limit covers minimum execution cost

### Policy Enforcement

The `PolicyEnforcer` validates transactions against configurable rules:

```csharp
var policy = PolicyConfig.RestrictedAccess(
    allowedWriters: new[] { address1, address2 },
    maxCalldataBytes: 128_000);
```

Violation types: `UnauthorizedSender`, `CalldataTooLarge`, `NonceTooLow`, `InsufficientBalance`

### Batch Production

Batches aggregate sequential blocks for efficient L1 anchoring:

```csharp
var batchConfig = BatchProductionConfig.WithCadence(blocksPerBatch: 100);
// or time-based:
var batchConfig = BatchProductionConfig.WithTimeThreshold(seconds: 60);
```

## Quick Start

```csharp
using Nethereum.AppChain.Sequencer;

var sequencerConfig = new SequencerConfig
{
    SequencerAddress = signerAddress,
    SequencerPrivateKey = privateKey,
    BlockTimeMs = 1000,
    MaxTransactionsPerBlock = 1000,
    AllowEmptyBlocks = false
};

var sequencer = new Sequencer(appChain, sequencerConfig, txPool, blockProducer);
await sequencer.StartAsync();

// Submit a transaction
var txHash = await sequencer.SubmitTransactionAsync(signedTransaction);
```

## Usage Examples

### Example 1: Create Sequencer with Policy

```csharp
using Nethereum.AppChain.Sequencer;

var config = new SequencerConfig
{
    SequencerAddress = signerAddress,
    SequencerPrivateKey = privateKey,
    BlockTimeMs = 500,
    Policy = PolicyConfig.RestrictedAccess(
        allowedWriters: new[] { userAddress1, userAddress2 },
        maxCalldataBytes: 64_000)
};

var policyEnforcer = new PolicyEnforcer(config.Policy);
var sequencer = new Sequencer(appChain, config, txPool, blockProducer,
    policyEnforcer: policyEnforcer);
await sequencer.StartAsync();
```

### Example 2: On-Demand Block Production

```csharp
var config = SequencerConfig.OnDemand;
config.SequencerAddress = signerAddress;
config.SequencerPrivateKey = privateKey;

var sequencer = new Sequencer(appChain, config, txPool, blockProducer);
await sequencer.StartAsync();

// Block produced immediately on transaction submission
await sequencer.SubmitTransactionAsync(signedTx);
```

### Example 3: Sequencer with Batch Production

```csharp
var config = new SequencerConfig
{
    SequencerAddress = signerAddress,
    SequencerPrivateKey = privateKey,
    BlockTimeMs = 1000,
    BatchProduction = BatchProductionConfig.WithCadence(blocksPerBatch: 100)
};

var batchProducer = new SequencerBatchProducer(
    blockStore, txStore, receiptStore, batchStore, config.BatchProduction);

var sequencer = new Sequencer(appChain, config, txPool, blockProducer,
    batchProducer: batchProducer);
await sequencer.StartAsync();
```

### Example 4: Coordinator for Sync-then-Produce

```csharp
var coordinator = new SequencerCoordinator(
    sequencer, liveBlockSync, peerManager, coordinatorConfig);

// Starts in sync mode, transitions to production when caught up
await coordinator.StartAsync();

coordinator.ModeChanged += (sender, args) =>
{
    Console.WriteLine($"Mode: {args.Mode}"); // Syncing → Producing
};
```

## API Reference

### Sequencer

Core sequencer orchestrating block production.

```csharp
public class Sequencer : ISequencer, IAsyncDisposable
{
    public Sequencer(IAppChain appChain, SequencerConfig config,
        ITxPool txPool, IBlockProducer blockProducer,
        IPolicyEnforcer? policyEnforcer = null,
        IBatchStore? batchStore = null,
        IBatchProducer? batchProducer = null,
        IBlockProductionStrategy? blockProductionStrategy = null);

    public Task StartAsync();
    public Task StopAsync();
    public Task<string> SubmitTransactionAsync(ISignedTransaction transaction);
    public Task ProduceBlockAsync();

    public event EventHandler<BlockProducedEventArgs>? BlockProduced;
}
```

### SequencerConfig

Operational parameters.

Key properties:
- `BlockTimeMs` (default: 1000) - Block production interval
- `MaxTransactionsPerBlock` (default: 1000) - Per-block transaction limit
- `MaxPoolSize` (default: 10000) - Transaction pool capacity
- `AllowEmptyBlocks` (default: false) - Whether to produce empty blocks
- `BlockProductionMode` - Interval or OnDemand

### PolicyEnforcer

Transaction validation against access control policies.

```csharp
public class PolicyEnforcer : IPolicyEnforcer
{
    public PolicyEnforcer(PolicyConfig policy);
    public Task<PolicyValidationResult> ValidateTransactionAsync(ISignedTransaction tx);
    public void UpdatePolicy(PolicyConfig newPolicy);
}
```

### AppChainNode

Full node wrapping IAppChain with optional sequencer.

```csharp
public class AppChainNode : ChainNodeBase
{
    public AppChainNode(IAppChain appChain, ISequencer? sequencer = null, IFilterStore? filterStore = null);
    public bool CanAcceptTransactions { get; }
    public Task<string> SendTransactionAsync(ISignedTransaction tx);
}
```

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - HTTP server hosting the sequencer
- **[Nethereum.AppChain.Metrics](../Nethereum.AppChain.Metrics/README.md)** - Instruments sequencer operations

### Dependencies
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Block production and transaction processing

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
