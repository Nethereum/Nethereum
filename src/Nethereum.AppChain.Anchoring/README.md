# Nethereum.AppChain.Anchoring

> **PREVIEW** — This package is in preview. APIs may change between releases.

L1 state root anchoring for [Nethereum AppChain](../Nethereum.AppChain/README.md) — anchored to Ethereum for trust.

## Overview

The anchoring service is what makes AppChain state more than just a private database. By periodically committing state roots, transaction roots, and receipt roots to an L1 anchor contract, it creates a cryptographic link between the AppChain and Ethereum. Anyone can verify that the AppChain state they synced matches what was committed to L1 — independently, without trusting the operator.

The package provides both a service for interacting with the anchor contract (`EvmAnchorService`) and a hosted worker (`AnchorWorker`) that automates periodic anchoring at configurable intervals. It also supports anchor verification, allowing any party to prove AppChain state against the L1-committed roots.

### Key Features

- **Periodic Anchoring**: Timer-based worker commits state at configurable cadence (every N blocks)
- **State Root Commitment**: Anchors state root, transaction root, and receipt root per block
- **Anchor Verification**: Verify L2 state against L1-committed roots for fraud proof support
- **Retry Mechanism**: Configurable retry with exponential backoff for L1 transaction failures
- **Force Anchor**: On-demand anchoring for immediate finality when needed

## Installation

```bash
dotnet add package Nethereum.AppChain.Anchoring
```

### Dependencies

- **Nethereum.CoreChain** - Block header access for state roots
- **Nethereum.Web3** - Web3 instance for L1 contract interaction
- **Nethereum.Contracts** - Smart contract function encoding/decoding
- **Microsoft.Extensions.Hosting.Abstractions** - `IHostedService` for background anchoring

## Key Concepts

### Anchor Cadence

Rather than anchoring every block (expensive on L1), the `AnchorWorker` commits state at a configurable cadence. For example, anchoring every 100 blocks means blocks 100, 200, 300, etc. get their roots committed to L1. Between anchors, blocks are "soft" (non-final).

### Finality Model

- **Soft blocks**: Produced by sequencer but not yet anchored to L1
- **Finalized blocks**: State roots committed to L1 anchor contract, cryptographically immutable
- **Verified blocks**: Third party has verified local state against L1 anchor

## Quick Start

```csharp
using Nethereum.AppChain.Anchoring;

var anchorConfig = new AnchorConfig
{
    Enabled = true,
    L1RpcUrl = "https://mainnet.infura.io/v3/YOUR-KEY",
    AnchorContractAddress = "0xYourAnchorContract",
    SequencerPrivateKey = sequencerKey,
    AnchorCadence = 100,
    AnchorIntervalSeconds = 60
};

var anchorService = new EvmAnchorService(web3, anchorConfig);
var lastAnchored = await anchorService.GetLatestAnchoredBlockAsync();
```

## Usage Examples

### Example 1: Anchor a Specific Block

```csharp
using Nethereum.AppChain.Anchoring;

var result = await anchorService.AnchorBlockAsync(
    blockNumber: 100,
    stateRoot: block.StateRoot,
    transactionsRoot: block.TransactionsHash,
    receiptsRoot: block.ReceiptHash);

Console.WriteLine($"Anchor TX: {result.AnchorTransactionHash}");
Console.WriteLine($"Status: {result.Status}"); // Submitted → Confirmed
```

### Example 2: Verify Anchor

```csharp
bool isValid = await anchorService.VerifyAnchorAsync(
    blockNumber: 100,
    stateRoot: localBlock.StateRoot,
    transactionsRoot: localBlock.TransactionsHash,
    receiptsRoot: localBlock.ReceiptHash);

Console.WriteLine(isValid ? "State matches L1 anchor" : "State mismatch!");
```

### Example 3: Hosted Worker with Force Anchor

```csharp
var worker = new AnchorWorker(anchorConfig, anchorService, appChain, logger);
await worker.StartAsync(cancellationToken);

// Force immediate anchor for a specific block
await worker.ForceAnchorAsync(blockNumber: 500);
```

## API Reference

### EvmAnchorService

L1 anchor contract interaction service.

```csharp
public class EvmAnchorService : IAnchorService
{
    public Task<AnchorInfo> AnchorBlockAsync(BigInteger blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot);
    public Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber);
    public Task<BigInteger> GetLatestAnchoredBlockAsync();
    public Task<bool> VerifyAnchorAsync(BigInteger blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot);
}
```

### AnchorWorker

Background hosted service for periodic anchoring.

```csharp
public class AnchorWorker : IHostedService
{
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync(CancellationToken ct);
    public Task ForceAnchorAsync(BigInteger? blockNumber = null);
}
```

### AnchorConfig

Key properties:
- `Enabled` - Enable/disable anchoring
- `L1RpcUrl` - Ethereum mainnet RPC endpoint
- `AnchorContractAddress` - L1 anchor contract address
- `AnchorCadence` (default: 100) - Blocks between anchors
- `AnchorIntervalSeconds` (default: 60) - Timer interval for checking

### AnchorInfo

Anchor state data:
- `BlockNumber`, `StateRoot`, `TransactionsRoot`, `ReceiptsRoot`
- `Status` - Pending, Submitted, Confirmed, Failed
- `AnchorTransactionHash` - L1 transaction hash
- `Timestamp` - When anchored

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - Server integrates anchor worker for automated anchoring
- **[Nethereum.AppChain.Sync](../Nethereum.AppChain.Sync/README.md)** - Sync uses anchors to determine finality

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Block header access
- **[Nethereum.Web3](../Nethereum.Web3/README.md)** - L1 contract interaction

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
