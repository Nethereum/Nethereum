---
name: devchain
description: Help users run a local Ethereum dev chain, write integration tests, fork networks, debug transactions, or replace Hardhat/Anvil with Nethereum DevChain (.NET). Use this skill when the user mentions local Ethereum node, dev chain, test chain, Hardhat replacement, Anvil replacement, in-process blockchain, snapshot/revert, debug_traceTransaction, account impersonation, or anything involving a local EVM for development and testing with C# or .NET.
user-invocable: true
---

# Nethereum DevChain

DevChain is a complete in-process Ethereum node for .NET. It runs the full EVM (up to Prague), mines blocks instantly, and needs no external dependencies — no Docker, no Geth, no Hardhat, no Anvil.

## When to Use This

- Running a local Ethereum node for development or testing
- Writing integration tests that need real EVM execution
- Replacing Hardhat or Anvil with a .NET-native solution
- Forking mainnet or L2s for local testing
- Debugging transaction execution at the opcode level
- Setting up Aspire-orchestrated dev environments

## Packages

```bash
dotnet add package Nethereum.DevChain          # Core in-process node
dotnet add package Nethereum.DevChain.Server   # HTTP JSON-RPC server
```

## Quick Start — In-Process Node

```csharp
using Nethereum.DevChain;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
var devChain = new DevChainNode();
await devChain.StartAsync(account);  // Pre-funds with 10,000 ETH

var web3 = devChain.CreateWeb3(account);  // In-process, no HTTP

// Standard Nethereum APIs work as-is
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0x70997970C51812dc3A010C7d01b50e0d17dc79C8", 1.0m);

devChain.Dispose();
```

## Key Classes

| Class | Package | Purpose |
|-------|---------|---------|
| `DevChainNode` | Nethereum.DevChain | Main in-process node |
| `DevChainConfig` | Nethereum.DevChain | Chain configuration (ChainId, AutoMine, BlockGasLimit, etc.) |
| `DevChainServerConfig` | Nethereum.DevChain.Server | HTTP server configuration |
| `DevAccountManager` | Nethereum.DevChain.Server | HD wallet account management |
| `DevChainHostedService` | Nethereum.DevChain.Server | ASP.NET Core hosted service |

## Configuration

```csharp
var config = new DevChainConfig
{
    ChainId = 1337,              // Default: 1337 (Hardhat preset: 31337)
    BlockGasLimit = 30_000_000,
    AutoMine = true,             // Mine instantly on each tx
    InitialBalance = BigInteger.Parse("10000000000000000000000")  // 10,000 ETH
};

var devChain = new DevChainNode(config);
```

Presets: `DevChainConfig.Default` (1337), `DevChainConfig.Hardhat` (31337), `DevChainConfig.Anvil` (31337).

## Factory Methods

```csharp
// One-liner
var devChain = await DevChainNode.CreateAndStartAsync(account);

// In-memory (no SQLite)
var devChain = DevChainNode.CreateInMemory();

// Multiple accounts
var devChain = new DevChainNode();
await devChain.StartAsync(alice, bob);
var accounts = await devChain.GenerateAndFundAccountsAsync(10);
```

## Snapshot/Revert (Test Isolation)

```csharp
var snapshot = await devChain.TakeSnapshotAsync();
// ... transactions ...
await devChain.RevertToSnapshotAsync(snapshot);  // Undo all changes
```

## State Manipulation

```csharp
await devChain.SetBalanceAsync(address, Web3.Convert.ToWei(1_000_000));
await devChain.SetNonceAsync(address, 100);
await devChain.SetCodeAsync(address, bytecode);
await devChain.SetStorageAtAsync(address, slot, value);
```

Via RPC: `hardhat_setBalance`, `hardhat_setCode`, `hardhat_setNonce`, `hardhat_setStorageAt` (also `anvil_*` aliases).

## Forking

```csharp
var config = new DevChainConfig
{
    ForkUrl = "https://eth.llamarpc.com",
    ForkBlockNumber = 19000000
};
var devChain = new DevChainNode(config);
await devChain.StartAsync(account);
```

CLI: `nethereum-devchain -f https://eth.llamarpc.com --fork-block 19000000`

## Time Manipulation

```csharp
devChain.DevConfig.AddTimeOffset(3600);           // Advance 1 hour
devChain.DevConfig.SetNextBlockTimestamp(1700000000); // Exact timestamp
await devChain.MineBlockAsync();
```

## Debug Tracing

```csharp
using Nethereum.CoreChain.Tracing;

var traceConfig = new OpcodeTraceConfig
{
    DisableMemory = true,
    DisableStack = false,
    DisableStorage = false
};

var trace = await devChain.TraceTransactionAsync(txHash, traceConfig);
```

## HTTP Server (CLI)

```bash
nethereum-devchain                    # Defaults: port 8545, 10 accounts, auto-mine
nethereum-devchain -p 8546 -a 20     # Custom port and accounts
nethereum-devchain -f <url>          # Fork mode
nethereum-devchain --persist ./data  # Persistent storage
```

## Embedding HTTP Server in ASP.NET

```csharp
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Hosting;
using Nethereum.DevChain.Server.Server;

var config = new DevChainServerConfig { ChainId = 31337, Storage = "memory" };
builder.Services.AddDevChainServer(config);
builder.Services.AddHostedService<DevChainHostedService>();
```

## Integration Test Pattern

```csharp
public class DevChainFixture : IAsyncLifetime
{
    public DevChainNode Node { get; private set; }
    public Account Alice { get; private set; }

    public async Task InitializeAsync()
    {
        Alice = new Account("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        Node = new DevChainNode(new DevChainConfig { ChainId = 31337 });
        await Node.StartAsync(Alice);
    }

    public Task DisposeAsync() { Node?.Dispose(); return Task.CompletedTask; }
}

// In test class: snapshot before each test, revert after
public async Task InitializeAsync() => _snapshot = await _fixture.Node.TakeSnapshotAsync();
public async Task DisposeAsync() => await _fixture.Node.RevertToSnapshotAsync(_snapshot);
```

## Supported RPC Methods

Standard Ethereum (`eth_*`, `net_*`, `web3_*`), plus:
- **Dev**: `evm_mine`, `evm_snapshot`, `evm_revert`, `evm_increaseTime`, `evm_setNextBlockTimestamp`
- **Hardhat**: `hardhat_setBalance`, `hardhat_setCode`, `hardhat_setNonce`, `hardhat_setStorageAt`, `hardhat_impersonateAccount`, `hardhat_stopImpersonatingAccount`
- **Anvil aliases**: `anvil_setBalance`, `anvil_setCode`, `anvil_setNonce`, `anvil_setStorageAt`, `anvil_mine`, `anvil_snapshot`, `anvil_revert`
- **Debug**: `debug_traceTransaction`, `debug_traceCall`

## Hardhat/Anvil Migration

Same default mnemonic (`test test test test test test test test test test test junk`), same derivation path (`m/44'/60'/0'/0/{index}`), same RPC methods, same chain ID (31337 with Hardhat/Anvil presets). Existing test scripts work without changes.

For full documentation, see: https://docs.nethereum.com/docs/devchain/overview
