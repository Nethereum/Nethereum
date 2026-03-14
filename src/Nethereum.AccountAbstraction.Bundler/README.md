# Nethereum.AccountAbstraction.Bundler

ERC-4337 bundler implementation with mempool management, UserOperation validation, bundle execution, reputation tracking, and ERC-7562 compliance checking.

## Overview

Nethereum.AccountAbstraction.Bundler implements the bundler side of the ERC-4337 Account Abstraction protocol. A bundler collects UserOperations from users, validates them, bundles them into transactions, and submits them to the EntryPoint contract via `handleOps`. This package provides the full bundler pipeline: mempool management, multi-phase validation, gas estimation, bundle building and execution, entity reputation tracking, and ERC-7562 opcode/storage rule enforcement.

The bundler supports three operational modes: simplified AppChain mode (whitelisted, no strict rules), standard mode (full ERC-7562 validation), and production mode (strict + reputation + minimum stake requirements).

### Key Features

- **Full ERC-4337 Bundler**: `IBundlerService` implements all spec-required RPC methods
- **Thread-Safe Mempool**: `InMemoryUserOpMempool` with TTL, per-sender limits, and priority ordering
- **ERC-7562 Validation**: EVM simulation detecting forbidden opcodes, storage violations, and entity rules
- **Bundle Execution**: Builds and submits bundles via `EntryPoint.handleOps()`
- **Reputation System**: Tracks included/failed operations per entity with ban/throttle support
- **BLS Aggregation**: Optional BLS signature aggregation for `handleAggregatedOps()`
- **Auto-Bundling**: Timer-based automatic bundle execution at configurable intervals

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler
```

### Dependencies

- **Nethereum.AccountAbstraction** - Core ERC-4337 types, UserOperation, EntryPoint service
- **Nethereum.Web3** - Web3 instance for chain interaction
- **Nethereum.Contracts** - Contract function encoding
- **Nethereum.Signer** - Signature verification
- **Nethereum.EVM** - EVM simulation for ERC-7562 validation

## Key Concepts

### Bundler Pipeline

1. **Receive**: UserOperation arrives via `SendUserOperationAsync`
2. **Validate**: Check nonce, signature, sender, EntryPoint support, blacklist/whitelist
3. **Simulate** (optional): Run ERC-7562 validation via EVM simulation
4. **Mempool**: Add to priority-ordered mempool
5. **Bundle**: Timer triggers bundle building from pending operations
6. **Execute**: Submit bundle to EntryPoint via `handleOps()`
7. **Track**: Update reputation based on inclusion/failure

### Mempool Management

The `InMemoryUserOpMempool` stores pending operations with:
- Priority ordering by `maxPriorityFeePerGas`
- Per-sender limits to prevent pool monopolization
- TTL-based expiration (default 30 minutes)
- State tracking: Pending → Submitted → Included/Failed

### ERC-7562 Validation

`ERC7562SimulationService` simulates UserOperation validation through the EVM to detect:
- Forbidden opcodes (BALANCE, ORIGIN, GASPRICE, etc.) per entity type
- Unauthorized storage access patterns
- CREATE/CREATE2 violations during validation
- Entity role boundary violations (sender, factory, paymaster, aggregator)

### Bundler Modes

```csharp
// Simplified: no strict rules, for private chains
var appChain = BundlerConfig.CreateAppChainConfig(entryPoint, beneficiary);

// Standard: full ERC-7562, for public testnets
var standard = BundlerConfig.CreateStandardConfig(entryPoint, beneficiary);

// Production: strict + reputation + min stake
var production = BundlerConfig.CreateProductionConfig(entryPoint, beneficiary);
```

## Quick Start

```csharp
using Nethereum.AccountAbstraction.Bundler;

var config = BundlerConfig.CreateStandardConfig(
    entryPointAddress, beneficiaryAddress);

var bundler = new BundlerService(web3, config);

// Submit a UserOperation
string userOpHash = await bundler.SendUserOperationAsync(userOp, entryPointAddress);

// Check status
var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);
```

## Usage Examples

### Example 1: Configure and Run Bundler

```csharp
using Nethereum.AccountAbstraction.Bundler;

var config = new BundlerConfig
{
    SupportedEntryPoints = new[] { entryPointAddress },
    BeneficiaryAddress = bundlerAddress,
    MaxBundleSize = 10,
    MaxMempoolSize = 1000,
    AutoBundleIntervalMs = 10000,
    EnableERC7562Validation = true,
    StrictValidation = true
};

var bundler = new BundlerService(web3, config);
// Auto-bundling starts automatically
```

### Example 2: Custom Mempool

```csharp
var mempool = new InMemoryUserOpMempool(
    maxSize: 5000, entryTtl: TimeSpan.FromMinutes(30));

var bundler = new BundlerService(web3, config, mempool: mempool);
```

### Example 3: Manual Bundle Execution

```csharp
// Disable auto-bundling
config.AutoBundleIntervalMs = 0;
var bundler = new BundlerService(web3, config);

// Submit operations
await bundler.SendUserOperationAsync(userOp1, entryPoint);
await bundler.SendUserOperationAsync(userOp2, entryPoint);

// Manually trigger bundle
var result = await bundler.ExecuteBundleAsync();
Console.WriteLine($"Bundled {result?.UserOpResults?.Length ?? 0} operations");
```

## API Reference

### BundlerService

Core bundler implementing `IBundlerServiceExtended`.

```csharp
public class BundlerService : IBundlerServiceExtended, IDisposable
{
    // ERC-4337 spec methods
    public Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint);
    public Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(PackedUserOperation userOp, string entryPoint);
    public Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash);
    public Task<UserOperationInfo?> GetUserOperationByHashAsync(string userOpHash);
    public Task<string[]> SupportedEntryPointsAsync();

    // Extended methods
    public Task<BundleExecutionResult?> ExecuteBundleAsync();
    public Task<BundlerStats> GetStatsAsync();
}
```

### InMemoryUserOpMempool

Thread-safe in-memory mempool.

```csharp
public class InMemoryUserOpMempool : IUserOpMempool
{
    public Task<bool> AddAsync(MempoolEntry entry);
    public Task<MempoolEntry?> GetAsync(string userOpHash);
    public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null);
    public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber);
    public Task MarkFailedAsync(string[] userOpHashes, string error);
    public Task PruneAsync();
}
```

### ERC7562SimulationService

EVM-based validation for opcode and storage rules.

```csharp
public class ERC7562SimulationService
{
    public Task<ERC7562ValidationResult> ValidateUserOperationAsync(PackedUserOperation userOp, string entryPoint);
}
```

### BundlerConfig

Key properties:
- `SupportedEntryPoints` - EntryPoint contract addresses
- `BeneficiaryAddress` - Address receiving bundle gas refunds
- `MaxBundleSize` (default: 10) - Operations per bundle
- `AutoBundleIntervalMs` (default: 10000) - Bundle trigger interval
- `EnableERC7562Validation` - Enable opcode/storage simulation
- `UnsafeMode` - Skip all validation (testing only)

## Related Packages

### Used By (Consumers)
- **[Nethereum.AccountAbstraction.Bundler.RpcServer](../Nethereum.AccountAbstraction.Bundler.RpcServer/README.md)** - Exposes bundler as JSON-RPC endpoint

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 types

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [ERC-7562: Account Abstraction Validation Scope Rules](https://eips.ethereum.org/EIPS/eip-7562)
- [Nethereum Documentation](https://docs.nethereum.com)
