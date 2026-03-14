# Nethereum.DevChain.Server

A local Ethereum development node with JSON-RPC server. Similar to Hardhat Network or Anvil but built entirely in .NET. Supports EVM opcodes up to the Prague hardfork.

## Overview

- HTTP JSON-RPC server on configurable port
- Pre-funded accounts from HD wallet (default mnemonic compatible with Hardhat/Anvil)
- Instant transaction mining (auto-mine) or configurable block intervals
- Full EVM execution up to Prague hardfork with Geth-compatible tracing
- SQLite storage by default with auto-cleanup on exit
- State forking from live networks
- Hardhat and Anvil-compatible RPC methods
- Account impersonation for testing

## Installation

### As a .NET Global Tool

```bash
dotnet tool install -g Nethereum.DevChain.Server
```

Then run:

```bash
nethereum-devchain
```

### From Source

```bash
dotnet run --project src/Nethereum.DevChain.Server
```

## Quick Start

```bash
# Start with defaults (port 8545, 10 accounts, SQLite, auto-mine)
nethereum-devchain

# Custom port and more accounts
nethereum-devchain -p 8546 -a 20

# Set account balance to 100 ETH
nethereum-devchain -e 100

# Interval mining (1 block per second)
nethereum-devchain -b 1000

# Fork mainnet
nethereum-devchain -f https://eth.llamarpc.com --fork-block 19000000

# Persist chain data between restarts
nethereum-devchain --persist ./mychain

# Use in-memory storage (no SQLite)
nethereum-devchain --in-memory
```

## Command-Line Options

```
USAGE: nethereum-devchain [OPTIONS]

SERVER:
  -p, --port <PORT>           Port to listen on (default: 8545)
      --host <HOST>           Host to bind to (default: 127.0.0.1)
  -v, --verbose               Enable verbose RPC logging

ACCOUNTS:
  -a, --accounts <NUM>        Number of accounts to generate (default: 10)
  -m, --mnemonic <MNEMONIC>   HD wallet mnemonic phrase
  -e, --balance <ETH>         Account balance in ETH (default: 10000)

CHAIN:
  -c, --chain-id <ID>         Chain ID (default: 31337)
  -b, --block-time <MS>       Block time in ms, 0 = auto-mine (default: 0)
      --gas-limit <GAS>       Block gas limit (default: 30000000)

FORK:
  -f, --fork <URL>            Fork from a remote RPC endpoint
      --fork-block <NUMBER>   Fork at a specific block number

STORAGE:
      --persist [DIR]         Persist chain data to disk (default: ./chaindata)
      --in-memory             Use in-memory storage instead of SQLite
```

Default storage is SQLite with auto-cleanup on exit. Use `--persist` to keep data between restarts.

## Storage Modes

| Mode | Flag | Blocks/TX/Receipts/Logs | State/Filters/Trie | Cleanup |
|------|------|------------------------|---------------------|---------|
| **SQLite** (default) | _(none)_ | SQLite (temp file) | In-Memory | Auto-delete on exit |
| **SQLite persistent** | `--persist` | SQLite (./chaindata) | In-Memory | Kept between restarts |
| **In-memory** | `--in-memory` | In-Memory | In-Memory | Lost on exit |

SQLite uses WAL journal mode for good read/write concurrency. State, filters, and trie nodes remain in-memory for fast snapshot/revert operations.

## Startup Banner

```
 _   _      _   _
| \ | | ___| |_| |__   ___ _ __ ___ _   _ _ __ ___
|  \| |/ _ \ __| '_ \ / _ \ '__/ _ \ | | | '_ ` _ \
| |\  |  __/ |_| | | |  __/ | |  __/ |_| | | | | | |
|_| \_|\___|\__|_| |_|\___|_|  \___|\__,_|_| |_| |_|

              DevChain Server v5.8.0

  RPC:        http://127.0.0.1:8545
  Chain ID:   31337
  Gas Limit:  30,000,000
  Mining:     auto-mine (instant)
  Storage:    SQLite (auto-cleanup on exit)

  Available Accounts
  ==================
  (0) 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266 (10000 ETH)
  (1) 0x70997970C51812dc3A010C7d01b50e0d17dc79C8 (10000 ETH)
  ...

  Private Keys
  ============
  (0) 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80
  (1) 0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d
  ...

  HD Wallet
  =========
  Mnemonic:        test test test test test test test test test test test junk
  Derivation Path: m/44'/60'/0'/0/x
```

## Configuration File

Create `appsettings.json` in the working directory:

```json
{
  "DevChain": {
    "Port": 8545,
    "Host": "127.0.0.1",
    "ChainId": 31337,
    "BlockGasLimit": 30000000,
    "AutoMine": true,
    "BlockTime": 0,
    "AccountCount": 10,
    "Mnemonic": "test test test test test test test test test test test junk",
    "Verbose": false,
    "Storage": "sqlite",
    "Persist": false,
    "DataDir": "./chaindata",
    "Fork": {
      "Url": null,
      "BlockNumber": null
    }
  }
}
```

## Supported RPC Methods

### Standard Ethereum

| Method | Description |
|--------|-------------|
| `web3_clientVersion` | Client version |
| `web3_sha3` | Keccak-256 hash |
| `net_version` | Network ID |
| `net_listening` | Listening status |
| `net_peerCount` | Peer count |
| `eth_chainId` | Chain ID |
| `eth_blockNumber` | Current block number |
| `eth_gasPrice` | Gas price |
| `eth_maxPriorityFeePerGas` | Priority fee |
| `eth_feeHistory` | Fee history |
| `eth_getBalance` | Account balance |
| `eth_getCode` | Contract code |
| `eth_getStorageAt` | Storage value |
| `eth_getTransactionCount` | Account nonce |
| `eth_getBlockByNumber` | Block by number |
| `eth_getBlockByHash` | Block by hash |
| `eth_getBlockReceipts` | All receipts in block |
| `eth_getTransactionByHash` | Transaction by hash |
| `eth_getTransactionReceipt` | Transaction receipt |
| `eth_sendRawTransaction` | Submit transaction |
| `eth_call` | Execute call |
| `eth_estimateGas` | Estimate gas |
| `eth_createAccessList` | Generate access list |
| `eth_getLogs` | Query logs |
| `eth_getProof` | Merkle proof |
| `eth_accounts` | List funded accounts |
| `eth_coinbase` | Coinbase address |
| `eth_syncing` | Sync status |
| `eth_mining` | Mining status |
| `eth_getBlockTransactionCountByHash` | Transaction count in block by hash |
| `eth_getBlockTransactionCountByNumber` | Transaction count in block by number |
| `eth_getTransactionByBlockHashAndIndex` | Transaction by block hash and index |
| `eth_getTransactionByBlockNumberAndIndex` | Transaction by block number and index |
| `eth_newFilter` | Create log filter |
| `eth_newBlockFilter` | Create new block filter |
| `eth_getFilterChanges` | Poll filter for changes |
| `eth_getFilterLogs` | Get all logs for filter |
| `eth_uninstallFilter` | Remove a filter |

### Development

| Method | Description |
|--------|-------------|
| `evm_mine` | Mine a block |
| `evm_snapshot` | Create state snapshot |
| `evm_revert` | Revert to snapshot |
| `evm_increaseTime` | Advance block time |
| `evm_setNextBlockTimestamp` | Set next block timestamp |

### Account Management

| Method | Description |
|--------|-------------|
| `hardhat_setBalance` | Set account balance |
| `hardhat_setCode` | Set contract code |
| `hardhat_setNonce` | Set account nonce |
| `hardhat_setStorageAt` | Set storage slot |
| `hardhat_impersonateAccount` | Impersonate account |
| `hardhat_stopImpersonatingAccount` | Stop impersonating |

### Debug

| Method | Description |
|--------|-------------|
| `debug_traceTransaction` | Trace mined transaction |
| `debug_traceCall` | Trace call without mining |

### Anvil Aliases

The following `anvil_*` aliases are registered for Foundry/Anvil compatibility:

| Anvil Method | Routes To |
|--------------|-----------|
| `anvil_setBalance` | `hardhat_setBalance` |
| `anvil_setCode` | `hardhat_setCode` |
| `anvil_setNonce` | `hardhat_setNonce` |
| `anvil_setStorageAt` | `hardhat_setStorageAt` |
| `anvil_mine` | `evm_mine` |
| `anvil_snapshot` | `evm_snapshot` |
| `anvil_revert` | `evm_revert` |

## Usage Examples

### Connect with Nethereum (C#)

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account(
    "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80",
    31337);
var web3 = new Web3(account, "http://127.0.0.1:8545");

var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance.Value)} ETH");
```

### Connect with ethers.js

```javascript
const { ethers } = require("ethers");

const provider = new ethers.JsonRpcProvider("http://127.0.0.1:8545");
const wallet = new ethers.Wallet(
    "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80",
    provider);

const balance = await provider.getBalance(wallet.address);
console.log(`Balance: ${ethers.formatEther(balance)} ETH`);
```

### Deploy Contract

```csharp
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi, bytecode, account.Address, new HexBigInteger(3000000));

Console.WriteLine($"Deployed at: {receipt.ContractAddress}");
```

### Fork Mainnet

```bash
nethereum-devchain -f https://eth.llamarpc.com --fork-block 19000000
```

When forking:
- State is fetched on-demand from the fork source
- Local transactions modify local state only
- Original fork state remains accessible

### Time Manipulation

```javascript
// Advance 1 day
await provider.send("evm_increaseTime", [86400]);
await provider.send("evm_mine", []);

// Set specific timestamp
await provider.send("evm_setNextBlockTimestamp", [1700000000]);
await provider.send("evm_mine", []);
```

### Snapshots

```javascript
const snapshotId = await provider.send("evm_snapshot", []);
// ... make changes ...
await provider.send("evm_revert", [snapshotId]);
```

### Account Impersonation

```javascript
await provider.send("hardhat_impersonateAccount", ["0xWhaleAddress"]);
// Send transactions as the impersonated account
await provider.send("hardhat_stopImpersonatingAccount", ["0xWhaleAddress"]);
```

### Trace Transaction

```javascript
const trace = await provider.send("debug_traceTransaction", [
    txHash,
    { enableMemory: true, disableStack: false }
]);

console.log(`Gas: ${trace.gas}, Steps: ${trace.structLogs.length}`);
```

## HTTP Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/` | JSON-RPC endpoint (single or batch requests) |
| GET | `/` | Health check: `{"status":"ok","version":"..."}` |

Max request body: 10MB. CORS enabled (any origin).

## Default Accounts

Using the default mnemonic `test test test test test test test test test test test junk`:

| Index | Address | Private Key |
|-------|---------|-------------|
| 0 | 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266 | 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80 |
| 1 | 0x70997970C51812dc3A010C7d01b50e0d17dc79C8 | 0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d |
| 2 | 0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC | 0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a |
| 3 | 0x90F79bf6EB2c4f870365E785982E1f101E93b906 | 0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6 |
| 4 | 0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65 | 0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a |

Each account is funded with 10,000 ETH by default. The mnemonic and derivation path (`m/44'/60'/0'/0/x`) are the same as Hardhat and Anvil.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                HTTP Server (ASP.NET Minimal)             │
├─────────────────────────────────────────────────────────┤
│                    RpcDispatcher                         │
├──────────┬──────────┬──────────┬──────────┬─────────────┤
│ Standard │   Dev    │  Debug   │ Hardhat  │   Anvil     │
│ Handlers │ Handlers │ Handlers │ Handlers │  Aliases    │
├─────────────────────────────────────────────────────────┤
│                    DevChainNode                          │
├──────────┬──────────────────────┬───────────────────────┤
│  Block   │  Transaction         │  State                │
│ Producer │  Processor           │  Store                │
├──────────┴──────────────────────┴───────────────────────┤
│               EVMSimulator (Nethereum.EVM)               │
├─────────────────────────────────────────────────────────┤
│  SQLite (blocks/tx/receipts/logs)  │  InMemory (state)  │
└─────────────────────────────────────────────────────────┘
```

## Standard Dev Chain Features

Nethereum DevChain implements the standard feature set expected from Ethereum development nodes (Hardhat Network, Anvil, etc.):

- Fork support from live networks
- Transaction tracing (Geth-compatible debug APIs)
- Account impersonation
- State snapshots and revert
- `evm_*` and `hardhat_*` RPC methods
- Same default mnemonic and derivation path as Hardhat/Anvil
- Pre-funded accounts with configurable balances

Additionally, Nethereum DevChain provides:

- **Native .NET integration** - embed directly in .NET applications and tests
- **SQLite default storage** - bounded memory usage for long-running sessions
- **Persistent storage** - keep chain state between restarts with `--persist`
- **Prague hardfork support** - latest EVM opcode support

## Aspire Integration

DevChain.Server integrates with .NET Aspire for orchestrated development environments. The `Nethereum.Aspire.DevChain` project wraps the server with Aspire service defaults:

```csharp
// AppHost/Program.cs
var devchain = builder.AddProject<Projects.Nethereum_Aspire_DevChain>("devchain");

var indexer = builder.AddProject<Projects.Nethereum_Aspire_Indexer>("indexer")
    .WithReference(devchain)
    .WaitFor(devchain);
```

The Aspire wrapper uses the same `AddDevChainServer(config)` DI extension and `DevChainHostedService`. Configuration via `appsettings.json` or environment variables works identically. The Aspire variant defaults to in-memory storage since lifecycle is managed by the orchestrator.

## Embedding in .NET Applications

Use `AddDevChainServer` directly in any ASP.NET application:

```csharp
var config = new DevChainServerConfig { ChainId = 31337, Storage = "memory" };
builder.Services.AddDevChainServer(config);
builder.Services.AddHostedService<DevChainHostedService>();
```

This registers `DevChainNode`, `RpcDispatcher`, `DevAccountManager`, and all storage providers as singletons. The `DevChainHostedService` handles startup and graceful shutdown.

## Related Packages

- **Nethereum.DevChain** - Core development chain library
- **Nethereum.CoreChain** - Blockchain infrastructure and storage interfaces
- **Nethereum.EVM** - EVM simulator
- **Nethereum.Geth** - Geth-compatible APIs

## Additional Resources

- [Nethereum Documentation](http://docs.nethereum.com)
- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
