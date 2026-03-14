---
name: aa-bundler
description: "Help users run an ERC-4337 bundler using Nethereum — set up in-process or standalone bundlers with mempool, validation, reputation, and JSON-RPC server. Use when the user mentions running a bundler, ERC-4337 bundler setup, BundlerService, BundlerConfig, bundler RPC server, mempool configuration, UserOperation validation, or bundler presets (AppChain, Standard, Production) in .NET/C#."
user-invocable: true
---

# Run an ERC-4337 Bundler

Nethereum includes a full ERC-4337 bundler — collects UserOperations, validates them, manages a mempool with reputation tracking, and submits bundles to the EntryPoint.

## When to Use This

- User wants to **run their own bundler** instead of using a third-party service
- User is building an **application chain** that needs an integrated bundler
- User needs to **configure bundler settings** (validation, mempool, reputation)
- User wants a **JSON-RPC endpoint** for the bundler

## Packages

```bash
dotnet add package Nethereum.AccountAbstraction.Bundler
dotnet add package Nethereum.AccountAbstraction.Bundler.RpcServer  # for JSON-RPC endpoint
```

## Quick Start

```csharp
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var web3 = new Web3(new Account(privateKey), "http://localhost:8545");

var config = BundlerConfig.CreateAppChainConfig(
    entryPoint: EntryPointAddresses.Latest,
    beneficiary: "0xBeneficiaryAddress");

var bundlerService = new BundlerService(web3, config);

// Submit a UserOperation
string userOpHash = await bundlerService.SendUserOperationAsync(
    packedUserOp, EntryPointAddresses.Latest);

// Trigger immediate bundling
BundleResult result = await bundlerService.ExecuteBundleAsync();

// Check receipt
var receipt = await bundlerService.GetUserOperationReceiptAsync(userOpHash);
```

## Configuration Presets

| Preset | Use Case | StrictValidation | ERC-7562 | AutoBundle |
|--------|----------|:---:|:---:|:---:|
| `CreateAppChainConfig` | Private chains | false | false | 1s |
| `CreateStandardConfig` | Public testnets | true | true | 10s |
| `CreateProductionConfig` | Mainnet | true | true | 10s + staking |

```csharp
var appConfig = BundlerConfig.CreateAppChainConfig(entryPoint, beneficiary);
var stdConfig = BundlerConfig.CreateStandardConfig(entryPoint, beneficiary);
var prodConfig = BundlerConfig.CreateProductionConfig(entryPoint, beneficiary);
```

## Key Config Properties

| Property | Default | Description |
|----------|---------|-------------|
| `MaxBundleSize` | 10 | Max UserOperations per bundle |
| `MaxMempoolSize` | 1000 | Max UserOperations in mempool |
| `AutoBundleIntervalMs` | 10000 | Bundle interval (0 = manual) |
| `StrictValidation` | true | Enforce ERC-4337 rules |
| `EnableERC7562Validation` | false | Trace-based opcode validation |
| `UnsafeMode` | false | Skip all validation (testing only) |
| `MinStake` | 1 ETH | Min stake for reputation |
| `WhitelistedAddresses` | empty | Exempt from reputation checks |
| `BlacklistedAddresses` | empty | Permanently rejected |

## JSON-RPC Server

```csharp
using Nethereum.AccountAbstraction.Bundler.RpcServer;

var rpcServer = new RpcServer();
rpcServer.AddBundlerHandlers(bundlerService);

// Or create bundler from config:
rpcServer.AddBundlerHandlers(web3, config);
```

### Standard RPC Methods

| Method | Description |
|--------|-------------|
| `eth_sendUserOperation` | Submit UserOperation |
| `eth_estimateUserOperationGas` | Estimate gas |
| `eth_getUserOperationByHash` | Look up by hash |
| `eth_getUserOperationReceipt` | Get receipt |
| `eth_supportedEntryPoints` | List EntryPoints |
| `eth_chainId` | Return chain ID |

### Debug Methods

| Method | Description |
|--------|-------------|
| `debug_bundler_sendBundleNow` | Force immediate bundle |
| `debug_bundler_dumpMempool` | Dump mempool contents |
| `debug_bundler_dumpReputation` | Dump reputation scores |
| `debug_bundler_setReputation` | Set reputation manually |

## Reputation and Staking

```csharp
var config = BundlerConfig.CreateProductionConfig(entryPoint, beneficiary);
config.WhitelistedAddresses.Add("0xYourPaymaster");  // trust your paymaster
config.BlacklistedAddresses.Add("0xBadFactory");      // block bad actors
```

## Common Mistakes

- **No beneficiary** — required, receives bundler fees
- **Wrong EntryPoint** — bundler only accepts ops targeting `SupportedEntryPoints`
- **AutoBundle=0 without manual bundling** — ops pile up, never submitted
- **UnsafeMode in production** — skips all validation, wastes gas on bad ops

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-run-bundler
