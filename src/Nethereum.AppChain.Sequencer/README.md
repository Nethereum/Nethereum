# Nethereum.AppChain.Sequencer

> **PREVIEW** — This package is in preview. APIs may change between releases.

Transaction ordering, block production, and policy enforcement for [Nethereum AppChain](../Nethereum.AppChain/README.md) networks.

## Overview

The sequencer is the centralised operator in an AppChain — your business, your rules. It accepts transactions, validates them against configurable policies, orders them into blocks, and produces blocks at configurable intervals or on demand. All produced state is publicly verifiable and synchronisable by any follower.

The sequencer integrates with pluggable block-production strategies (single-sequencer or Clique PoA) and drives a background block-production loop with a circuit breaker that backs off under sustained failure.

### Key Features

- **Transaction Validation Pipeline**: Nonce checking, balance validation, intrinsic gas calculation, and sender recovery
- **Configurable Block Production**: Interval-based or on-demand block production modes
- **Policy Enforcement**: Pluggable access control with sender allowlists and transaction size limits
- **Pluggable Block-Production Strategy**: Single-sequencer or Clique PoA turn-based production
- **Circuit Breaker**: After 10 consecutive block-production failures, delays ~30s and resets rather than halting

## Installation

```bash
dotnet add package Nethereum.AppChain.Sequencer
```

### Dependencies

Direct project references:

- **Nethereum.AppChain** - `IAppChain` and `AppChainConfig`
- **Nethereum.AppChain.Anchoring** - message queue and processor abstractions for L1 anchoring
- **Nethereum.Web3** - Web3 client surface

`BlockProducer`, `TransactionProcessor`, `ITxPool`, and `IBlockProductionStrategy` are provided by this package and `Nethereum.CoreChain` (a transitive dependency). Transaction signing and model types (`Nethereum.Signer`, `Nethereum.Model`) are transitive.

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

1. **Policy enforcement** - Check sender authorization and transaction size limits
2. **Sender recovery** - Recover sender address from ECDSA signature
3. **Nonce checking** - Verify nonce matches expected next nonce
4. **Balance validation** - Ensure sender has sufficient funds for value + gas
5. **Intrinsic gas calculation** - Verify gas limit covers minimum execution cost

### Policy Enforcement

The `PolicyEnforcer` validates transactions against configurable rules. `RestrictedAccess` takes the allowed-writers list; the calldata/size limit is set on `PolicyConfig.MaxCalldataBytes` (default `128_000`):

```csharp
var policy = PolicyConfig.RestrictedAccess(
    allowedWriters: new List<string> { address1, address2 });
policy.MaxCalldataBytes = 128_000;
```

Violation types: `UnauthorizedSender`, `CalldataTooLarge`, `BlacklistedAddress`, `InvalidSignature`, `NonceTooLow`, `InsufficientBalance`

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

// txPool, blockProducer and the policy enforcer are created automatically
// from the config when not supplied.
var sequencer = new Sequencer(appChain, sequencerConfig);
await sequencer.StartAsync();

// Submit a transaction (returns the transaction hash bytes)
byte[] txHash = await sequencer.SubmitTransactionAsync(signedTransaction);
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
        allowedWriters: new List<string> { userAddress1, userAddress2 })
};
config.Policy.MaxCalldataBytes = 64_000;

// PolicyEnforcer requires both the policy and the chain.
var policyEnforcer = new PolicyEnforcer(config.Policy, appChain);
var sequencer = new Sequencer(appChain, config, policyEnforcer: policyEnforcer);
await sequencer.StartAsync();
```

### Example 2: On-Demand Block Production

```csharp
var config = SequencerConfig.OnDemand;
config.SequencerAddress = signerAddress;
config.SequencerPrivateKey = privateKey;

var sequencer = new Sequencer(appChain, config);
await sequencer.StartAsync();

// Block produced immediately on transaction submission
await sequencer.SubmitTransactionAsync(signedTx);
```

### Example 3: Reacting to Produced Blocks

```csharp
var sequencer = new Sequencer(appChain, SequencerConfig.Default);

sequencer.BlockProduced += (sender, result) =>
{
    Console.WriteLine($"Block {result.Header.BlockNumber} produced with {result.TransactionResults.Count} txs");
};

await sequencer.StartAsync();
```

## API Reference

### Sequencer

Core sequencer orchestrating block production. All constructor parameters after `config` are optional; when omitted, the transaction pool, block producer, and policy enforcer are built from the config.

```csharp
public class Sequencer : ISequencer, IAsyncDisposable
{
    public Sequencer(
        IAppChain appChain,
        SequencerConfig config,
        ITxPool? txPool = null,
        IBlockProducer? blockProducer = null,
        IPolicyEnforcer? policyEnforcer = null,
        IBlockProductionStrategy? blockProductionStrategy = null,
        IMessageQueue? messageQueue = null,
        IMessageProcessor? messageProcessor = null,
        ILogger<Sequencer>? logger = null,
        string? nodeId = null,
        IIncrementalStateRootCalculator? stateRootCalculator = null,
        IBlockAccessListStore? blockAccessListStore = null);

    public SequencerConfig Config { get; }
    public IAppChain AppChain { get; }
    public ITxPool TxPool { get; }
    public IPolicyEnforcer PolicyEnforcer { get; }
    public IBlockProductionStrategy? BlockProductionStrategy { get; }

    public Task StartAsync(CancellationToken cancellationToken = default);
    public Task StopAsync();
    public Task<byte[]> SubmitTransactionAsync(ISignedTransaction transaction);
    public Task<byte[]> ProduceBlockAsync();
    public Task<BigInteger> GetBlockNumberAsync();
    public Task<BlockHeader?> GetLatestBlockAsync();
    public ValueTask DisposeAsync();

    public event EventHandler<BlockProductionResult>? BlockProduced;
}
```

### SequencerConfig

Operational parameters.

Key properties:
- `BlockTimeMs` (default: 1000) - Block production interval
- `MaxTransactionsPerBlock` (default: 1000) - Per-block transaction limit
- `MaxMessagesPerBlock` (default: 50) - Per-block L1 anchoring message limit
- `MaxPoolSize` (default: 50_000) - Transaction pool capacity
- `MaxTxsPerSender` (default: 1_000) - Per-sender pending transaction limit
- `AllowEmptyBlocks` (default: false) - Whether to produce empty blocks
- `BlockProductionMode` - Interval or OnDemand

Factories: `SequencerConfig.Default` (1000ms interval) and `SequencerConfig.OnDemand` (immediate production).

### PolicyEnforcer

Transaction validation against access control policies. The constructor requires both the policy and the chain.

```csharp
public class PolicyEnforcer : IPolicyEnforcer
{
    public PolicyEnforcer(PolicyConfig policy, IAppChain appChain);

    public PolicyConfig Policy { get; }

    public Task<PolicyValidationResult> ValidateTransactionAsync(ISignedTransaction tx);
    public void UpdatePolicy(PolicyConfig newPolicy);
    public void UpdateWritersRoot(byte[] writersRoot);
}
```

### AppChainNode

Full node wrapping `IAppChain` with an optional sequencer.

```csharp
public class AppChainNode : ChainNodeBase
{
    public AppChainNode(IAppChain appChain, ISequencer? sequencer = null, IFilterStore? filterStore = null);
    public AppChainNode(IAppChain appChain, ISequencer? sequencer, IFilterStore? filterStore,
        IBlockAccessListStore? blockAccessListStore);

    public IAppChain AppChain { get; }
    public ISequencer? Sequencer { get; }
    public bool CanAcceptTransactions { get; }
    public override ChainConfig Config { get; }

    public override Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx);
    public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync();
    public Task<byte[]> ProduceBlockAsync();
}
```

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - HTTP server hosting the sequencer

### Dependencies
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core chain abstraction
- **[Nethereum.AppChain.Anchoring](../Nethereum.AppChain.Anchoring/README.md)** - L1 anchoring message queue and processor
- **[Nethereum.Web3](../Nethereum.Web3/README.md)** - Web3 client surface

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
