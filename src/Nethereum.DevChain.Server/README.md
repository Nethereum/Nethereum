# Nethereum.DevChain.Server

A local Ethereum development node with JSON-RPC server. Similar to Ganache, Hardhat Network, or Anvil but built entirely in .NET.

## Overview

Nethereum.DevChain.Server provides:
- HTTP JSON-RPC server on configurable port
- Pre-funded accounts from HD wallet
- Instant transaction mining
- Full EVM execution with tracing
- Geth-compatible debug APIs
- State forking from live networks
- Hardhat-compatible impersonation

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
cd src/Nethereum.DevChain.Server
dotnet run
```

## Quick Start

```bash
# Start with defaults (port 8545, 10 accounts)
nethereum-devchain

# Custom port
nethereum-devchain --port 8546

# Custom chain ID
nethereum-devchain --chain-id 31337

# More accounts
nethereum-devchain --accounts 20

# Custom mnemonic
nethereum-devchain --mnemonic "your twelve word mnemonic phrase here"

# Fork from mainnet
nethereum-devchain --fork https://mainnet.infura.io/v3/YOUR_KEY

# Fork at specific block
nethereum-devchain --fork https://mainnet.infura.io/v3/YOUR_KEY --fork-block 18000000

# Verbose logging
nethereum-devchain --verbose
```

## Startup Banner

When started, the server displays:

```
 _   _      _   _
| \ | | ___| |_| |__   ___ _ __ ___ _   _ _ __ ___
|  \| |/ _ \ __| '_ \ / _ \ '__/ _ \ | | | '_ ` _ \
| |\  |  __/ |_| | | |  __/ | |  __/ |_| | | | | | |
|_| \_|\___|\__|_| |_|\___|_|  \___|\__,_|_| |_| |_|

              DevChain Server v1.0.0

RPC Server listening on http://127.0.0.1:8545
Chain ID: 1337

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
Mnemonic: test test test test test test test test test test test junk
Derivation Path: m/44'/60'/0'/0/x
```

## Command-Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--port` | `-p` | RPC server port | 8545 |
| `--host` | | Server host | 127.0.0.1 |
| `--chain-id` | `-c` | Chain ID | 1337 |
| `--accounts` | `-a` | Number of accounts | 10 |
| `--mnemonic` | | HD wallet mnemonic | test mnemonic |
| `--fork` | | Fork URL | - |
| `--fork-block` | | Fork block number | latest |
| `--verbose` | `-v` | Verbose logging | false |

## Configuration File

Create `appsettings.json` in the working directory:

```json
{
  "DevChain": {
    "Port": 8545,
    "Host": "127.0.0.1",
    "ChainId": 1337,
    "AccountCount": 10,
    "Mnemonic": "test test test test test test test test test test test junk",
    "Verbose": false,
    "Fork": {
      "Url": null,
      "BlockNumber": null
    }
  }
}
```

**From:** `src/Nethereum.DevChain.Server/Configuration/DevChainServerConfig.cs`

## Supported RPC Methods

### Standard Ethereum Methods

| Method | Description |
|--------|-------------|
| `web3_clientVersion` | Client version |
| `net_version` | Network ID |
| `eth_chainId` | Chain ID |
| `eth_blockNumber` | Current block number |
| `eth_gasPrice` | Gas price |
| `eth_getBalance` | Account balance |
| `eth_getCode` | Contract code |
| `eth_getStorageAt` | Storage value |
| `eth_getTransactionCount` | Account nonce |
| `eth_getBlockByNumber` | Block by number |
| `eth_getBlockByHash` | Block by hash |
| `eth_getTransactionByHash` | Transaction by hash |
| `eth_getTransactionReceipt` | Transaction receipt |
| `eth_sendRawTransaction` | Submit transaction |
| `eth_call` | Execute call |
| `eth_estimateGas` | Estimate gas |
| `eth_getLogs` | Query logs |
| `eth_getProof` | Merkle proof |
| `eth_feeHistory` | Fee history |
| `eth_maxPriorityFeePerGas` | Priority fee |
| `eth_accounts` | List accounts |

### Development Methods

| Method | Description |
|--------|-------------|
| `evm_mine` | Mine a block |
| `evm_snapshot` | Create snapshot |
| `evm_revert` | Revert to snapshot |
| `evm_increaseTime` | Advance time |
| `evm_setNextBlockTimestamp` | Set next timestamp |

### Debug Methods

| Method | Description |
|--------|-------------|
| `debug_traceTransaction` | Trace transaction |
| `debug_traceCall` | Trace call |

### Hardhat Methods

| Method | Description |
|--------|-------------|
| `hardhat_impersonateAccount` | Impersonate account |
| `hardhat_stopImpersonatingAccount` | Stop impersonating |

## Usage Examples

### Connect with Web3

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

// Connect to local dev chain
var account = new Account("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80", 1337);
var web3 = new Web3(account, "http://127.0.0.1:8545");

// Get balance
var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance.Value)} ETH");
```

### Connect with ethers.js

```javascript
const { ethers } = require("ethers");

const provider = new ethers.JsonRpcProvider("http://127.0.0.1:8545");
const wallet = new ethers.Wallet("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80", provider);

const balance = await provider.getBalance(wallet.address);
console.log(`Balance: ${ethers.formatEther(balance)} ETH`);
```

### Deploy Contract

```csharp
var deployReceipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi,
    bytecode,
    account.Address,
    new HexBigInteger(3000000) // gas
);

Console.WriteLine($"Contract deployed at: {deployReceipt.ContractAddress}");
```

### Trace Transaction

```javascript
const trace = await provider.send("debug_traceTransaction", [
  txHash,
  { enableMemory: true, disableStack: false }
]);

console.log(`Gas used: ${trace.gas}`);
console.log(`Steps: ${trace.structLogs.length}`);
```

### Fork Mainnet

```bash
# Fork mainnet at latest block
nethereum-devchain --fork https://mainnet.infura.io/v3/YOUR_KEY

# Fork at specific block for reproducibility
nethereum-devchain --fork https://mainnet.infura.io/v3/YOUR_KEY --fork-block 18000000
```

When forking:
- State is fetched on-demand from the fork source
- Local transactions modify local state only
- Original fork state remains accessible

### Impersonate Account

```javascript
// Start impersonating a whale
await provider.send("hardhat_impersonateAccount", ["0xWhaleAddress"]);

// Send transaction as the whale (no signature needed)
const tx = await wallet.sendTransaction({
  from: "0xWhaleAddress",
  to: recipient,
  value: ethers.parseEther("1000")
});

// Stop impersonating
await provider.send("hardhat_stopImpersonatingAccount", ["0xWhaleAddress"]);
```

### Time Manipulation

```javascript
// Advance time by 1 day
await provider.send("evm_increaseTime", [86400]);
await provider.send("evm_mine", []);

// Or set specific timestamp for next block
await provider.send("evm_setNextBlockTimestamp", [Math.floor(Date.now() / 1000) + 86400]);
await provider.send("evm_mine", []);
```

### Snapshots

```javascript
// Take snapshot
const snapshotId = await provider.send("evm_snapshot", []);

// Do some transactions...
await contract.doSomething();

// Revert to snapshot (undoes all changes)
await provider.send("evm_revert", [snapshotId]);
```

## Default Accounts

Using the default mnemonic `test test test test test test test test test test test junk`:

| Index | Address | Private Key |
|-------|---------|-------------|
| 0 | 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266 | 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80 |
| 1 | 0x70997970C51812dc3A010C7d01b50e0d17dc79C8 | 0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d |
| 2 | 0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC | 0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a |
| 3 | 0x90F79bf6EB2c4f870365E785982E1f101E93b906 | 0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6 |
| 4 | 0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65 | 0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a |

Each account is funded with 10,000 ETH by default.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  HTTP Server (ASP.NET)                   │
├─────────────────────────────────────────────────────────┤
│                    RpcDispatcher                         │
├─────────────────────────────────────────────────────────┤
│  Standard    │    Dev        │    Debug      │ Hardhat  │
│  Handlers    │    Handlers   │    Handlers   │ Handlers │
├─────────────────────────────────────────────────────────┤
│                    DevChainNode                          │
├─────────────────────────────────────────────────────────┤
│  BlockManager │ TransactionProcessor │ StateStore       │
├─────────────────────────────────────────────────────────┤
│                    EVMSimulator                          │
└─────────────────────────────────────────────────────────┘
```

## Comparison with Other Tools

| Feature | Nethereum DevChain | Ganache | Hardhat | Anvil |
|---------|-------------------|---------|---------|-------|
| Language | .NET | JavaScript | JavaScript | Rust |
| Fork Support | ✅ | ✅ | ✅ | ✅ |
| Tracing | ✅ | ✅ | ✅ | ✅ |
| Impersonation | ✅ | ✅ | ✅ | ✅ |
| Snapshots | ✅ | ✅ | ✅ | ✅ |
| .NET Integration | Native | Via RPC | Via RPC | Via RPC |

## Related Packages

- **Nethereum.DevChain** - Core development chain library
- **Nethereum.CoreChain** - Blockchain infrastructure
- **Nethereum.EVM** - EVM simulator
- **Nethereum.Geth** - Geth-compatible APIs

## Additional Resources

- [Nethereum Documentation](http://docs.nethereum.com)
- [Ethereum JSON-RPC Specification](https://ethereum.github.io/execution-apis/api-documentation/)
