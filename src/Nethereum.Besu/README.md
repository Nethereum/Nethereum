# Nethereum.Besu

Extended Web3 library for Hyperledger Besu client. Provides RPC client methods for Admin, Debug, Miner, Clique, IBFT, EEA (private transactions), and Permissioning APIs.

## Overview

Nethereum.Besu extends `Nethereum.Web3` with Besu-specific JSON-RPC methods. Use `Web3Besu` instead of `Web3` to access additional APIs for node administration, consensus mechanisms (Clique/IBFT), permissioning, and private transactions.

**API Services:**
- **Admin** - Peer management (add/remove peers)
- **Debug** - Transaction tracing, storage inspection, metrics
- **Miner** - Mining control (start/stop)
- **Clique** - Clique PoA consensus (propose/discard signers)
- **IBFT** - IBFT PoA consensus (validator voting)
- **Permissioning** - Node and account whitelisting
- **EEA** - Enterprise Ethereum Alliance private transactions
- **TxPool** - Transaction pool statistics and inspection

## Installation

```bash
dotnet add package Nethereum.Besu
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Besu
```

## Dependencies

**Package References:**
- Nethereum.RPC
- Nethereum.Web3

## Usage

### Web3Besu Initialization

Replace `Web3` with `Web3Besu`:

```csharp
using Nethereum.Besu;

var web3 = new Web3Besu("http://localhost:8545");
```

With account:

```csharp
using Nethereum.Besu;
using Nethereum.Web3.Accounts;

var account = new Account("PRIVATE_KEY");
var web3 = new Web3Besu(account, "http://localhost:8545");
```

**From:** `src/Nethereum.Besu/Web3Besu.cs:10`

## Admin API

Manage node peers.

### Get Peers

```csharp
var peers = await web3.Admin.Peers.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/IAdminApiService.cs`

### Add/Remove Peer

```csharp
var enode = "enode://pubkey@ip:port";
var added = await web3.Admin.AddPeer.SendRequestAsync(enode);

var removed = await web3.Admin.RemovePeer.SendRequestAsync(enode);
```

**From:** `src/Nethereum.Besu/IAdminApiService.cs`

### Get Node Info

```csharp
var nodeInfo = await web3.Admin.NodeInfo.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/IAdminApiService.cs`

## Debug API

Transaction tracing, storage inspection, and metrics.

### Trace Transaction

```csharp
var txHash = "0x...";
var trace = await web3.DebugBesu.TraceTransaction.SendRequestAsync(txHash);
```

**From:** `src/Nethereum.Besu/IDebugApiService.cs`

### Get Storage Range

Inspect contract storage at specific block/transaction/address:

```csharp
using Nethereum.Hex.HexTypes;

var blockHash = "0x...";
var txIndex = 0;
var address = "0xContractAddress";
var startKey = "0x0000000000000000000000000000000000000000000000000000000000000000";
var limit = 100;

var storageRange = await web3.DebugBesu.StorageRangeAt.SendRequestAsync(
    blockHash,
    txIndex,
    address,
    startKey,
    limit);
```

**From:** `src/Nethereum.Besu/RPC/BesuDebug/DebugStorageRangeAt.cs`

### Get Metrics

```csharp
var metrics = await web3.DebugBesu.Metrics.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/RPC/BesuDebug/DebugMetrics.cs`

## Miner API

Control mining operations.

### Start/Stop Mining

```csharp
// Start mining
var started = await web3.Miner.Start.SendRequestAsync();

// Stop mining
var stopped = await web3.Miner.Stop.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/RPC/Miner/MinerStart.cs`

## Clique API

Clique Proof-of-Authority consensus control.

### Get Signers

```csharp
// Get current signers
var signers = await web3.Clique.GetSigners.SendRequestAsync();

// Get signers at specific block hash
var signersAtHash = await web3.Clique.GetSignersAtHash.SendRequestAsync("0xBlockHash");
```

**From:** `src/Nethereum.Besu/ICliqueApiService.cs:8-9`

### Propose Signer

```csharp
// Propose adding a signer (auth = true)
var proposed = await web3.Clique.Propose.SendRequestAsync("0xNewSignerAddress", true);

// Propose removing a signer (auth = false)
var proposedRemoval = await web3.Clique.Propose.SendRequestAsync("0xSignerAddress", false);
```

**From:** `src/Nethereum.Besu/RPC/Clique/CliquePropose.cs`

### Discard Proposal

```csharp
var discarded = await web3.Clique.Discard.SendRequestAsync("0xSignerAddress");
```

**From:** `src/Nethereum.Besu/RPC/Clique/CliqueDiscard.cs`

### Get Proposals

```csharp
var proposals = await web3.Clique.Proposals.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/RPC/Clique/CliqueProposals.cs`

## IBFT API

Istanbul Byzantine Fault Tolerance consensus control.

### Get Validators

```csharp
using Nethereum.RPC.Eth.DTOs;

// Get validators by block number
var validators = await web3.Ibft.GetValidatorsByBlockNumber.SendRequestAsync(
    BlockParameter.CreateLatest());

// Get validators by block hash
var validatorsByHash = await web3.Ibft.GetValidatorsByBlockHash.SendRequestAsync("0xBlockHash");
```

**From:** `src/Nethereum.Besu/IbftApiService.cs:12-10`

### Propose Validator Vote

```csharp
// Propose adding validator (add = true)
var proposed = await web3.Ibft.ProposeValidatorVote.SendRequestAsync("0xValidatorAddress", true);

// Propose removing validator (add = false)
var proposedRemoval = await web3.Ibft.ProposeValidatorVote.SendRequestAsync("0xValidatorAddress", false);
```

**From:** `src/Nethereum.Besu/RPC/IBFT/IbftProposeValidatorVote.cs`

### Discard Validator Vote

```csharp
var discarded = await web3.Ibft.DiscardValidatorVote.SendRequestAsync("0xValidatorAddress");
```

**From:** `src/Nethereum.Besu/RPC/IBFT/IbftDiscardValidatorVote.cs`

### Get Pending Votes

```csharp
var pendingVotes = await web3.Ibft.GetPendingVotes.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/RPC/IBFT/IbftGetPendingVotes.cs`

## Permissioning API

Node and account whitelisting for permissioned networks.

### Node Whitelisting

```csharp
// Get node whitelist
var nodeWhitelist = await web3.Permissioning.GetNodesWhitelist.SendRequestAsync();

// Add nodes to whitelist
var nodesToAdd = new[] { "enode://pubkey1@ip1:port1", "enode://pubkey2@ip2:port2" };
var nodesAdded = await web3.Permissioning.AddNodesToWhitelist.SendRequestAsync(nodesToAdd);

// Remove nodes from whitelist
var nodesToRemove = new[] { "enode://pubkey1@ip1:port1" };
var nodesRemoved = await web3.Permissioning.RemoveNodesFromWhitelist.SendRequestAsync(nodesToRemove);
```

**From:** `src/Nethereum.Besu/IPermissioningApiService.cs:11-12`

### Account Whitelisting

```csharp
// Get account whitelist
var accountWhitelist = await web3.Permissioning.GetAccountsWhitelist.SendRequestAsync();

// Add accounts to whitelist
var accountsToAdd = new[] { "0xAddress1", "0xAddress2" };
var accountsAdded = await web3.Permissioning.AddAccountsToWhitelist.SendRequestAsync(accountsToAdd);

// Remove accounts from whitelist
var accountsToRemove = new[] { "0xAddress1" };
var accountsRemoved = await web3.Permissioning.RemoveAccountsFromWhitelist.SendRequestAsync(accountsToRemove);
```

**From:** `src/Nethereum.Besu/IPermissioningApiService.cs:7-9`

### Reload Permissions

Reload permissions from configuration file:

```csharp
var reloaded = await web3.Permissioning.ReloadPermissionsFromFile.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/RPC/Permissioning/PermReloadPermissionsFromFile.cs`

## EEA API

Enterprise Ethereum Alliance private transaction support.

### Send Private Transaction

```csharp
// Send signed private transaction
var signedPrivateTx = "0x...";
var txHash = await web3.Eea.SendRawTransaction.SendRequestAsync(signedPrivateTx);
```

**From:** `src/Nethereum.Besu/RPC/EEA/EeaSendRawTransaction.cs`

### Get Private Transaction Receipt

```csharp
var privateTxHash = "0x...";
var receipt = await web3.Eea.GetTransactionReceipt.SendRequestAsync(privateTxHash);
```

**From:** `src/Nethereum.Besu/RPC/EEA/EeaGetTransactionReceipt.cs`

## TxPool API

Transaction pool statistics and inspection.

### Get Pool Statistics

```csharp
var stats = await web3.TxPool.PantheonStatistics.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/ITxPoolApiService.cs`

### Get Pool Transactions

```csharp
var transactions = await web3.TxPool.PantheonTransactions.SendRequestAsync();
```

**From:** `src/Nethereum.Besu/ITxPoolApiService.cs`

## API Reference

### Admin API Service

**Interface:** `IAdminApiService` (`src/Nethereum.Besu/IAdminApiService.cs`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| Peers | admin_peers | List connected peers |
| NodeInfo | admin_nodeInfo | Get node information |
| AddPeer | admin_addPeer | Add peer by enode URL |
| RemovePeer | admin_removePeer | Remove peer by enode URL |

### Debug API Service

**Interface:** `IDebugApiService` (`src/Nethereum.Besu/IDebugApiService.cs`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| TraceTransaction | debug_traceTransaction | Trace transaction execution |
| StorageRangeAt | debug_storageRangeAt | Get contract storage range |
| Metrics | debug_metrics | Get node metrics |

### Miner API Service

**Interface:** `IMinerApiService` (`src/Nethereum.Besu/MinerApiService.cs`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| Start | miner_start | Start mining |
| Stop | miner_stop | Stop mining |

### Clique API Service

**Interface:** `ICliqueApiService` (`src/Nethereum.Besu/ICliqueApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| GetSigners | clique_getSigners | Get current signers |
| GetSignersAtHash | clique_getSignersAtHash | Get signers at block hash |
| Propose | clique_propose | Propose adding/removing signer |
| Discard | clique_discard | Discard signer proposal |
| Proposals | clique_proposals | Get current proposals |

### IBFT API Service

**Interface:** `IIbftApiService` (`src/Nethereum.Besu/IbftApiService.cs:7`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| GetValidatorsByBlockNumber | ibft_getValidatorsByBlockNumber | Get validators by block number |
| GetValidatorsByBlockHash | ibft_getValidatorsByBlockHash | Get validators by block hash |
| ProposeValidatorVote | ibft_proposeValidatorVote | Propose adding/removing validator |
| DiscardValidatorVote | ibft_discardValidatorVote | Discard validator vote |
| GetPendingVotes | ibft_getPendingVotes | Get pending validator votes |

### Permissioning API Service

**Interface:** `IPermissioningApiService` (`src/Nethereum.Besu/IPermissioningApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| GetNodesWhitelist | perm_getNodesWhitelist | Get node whitelist |
| AddNodesToWhitelist | perm_addNodesToWhitelist | Add nodes to whitelist |
| RemoveNodesFromWhitelist | perm_removeNodesFromWhitelist | Remove nodes from whitelist |
| GetAccountsWhitelist | perm_getAccountsWhitelist | Get account whitelist |
| AddAccountsToWhitelist | perm_addAccountsToWhitelist | Add accounts to whitelist |
| RemoveAccountsFromWhitelist | perm_removeAccountsFromWhitelist | Remove accounts from whitelist |
| ReloadPermissionsFromFile | perm_reloadPermissionsFromFile | Reload permissions from file |

### EEA API Service

**Interface:** `IEeaApiService` (`src/Nethereum.Besu/IEeaApiService.cs:5`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| SendRawTransaction | eea_sendRawTransaction | Send signed private transaction |
| GetTransactionReceipt | eea_getTransactionReceipt | Get private transaction receipt |

### TxPool API Service

**Interface:** `ITxPoolApiService` (`src/Nethereum.Besu/ITxPoolApiService.cs`)

| Method | RPC Method | Description |
|--------|-----------|-------------|
| PantheonStatistics | txpool_besuStatistics | Get transaction pool statistics |
| PantheonTransactions | txpool_besuTransactions | Get transaction pool transactions |

## Related Packages

- **Nethereum.Web3** - Base Web3 implementation
- **Nethereum.RPC** - JSON-RPC client infrastructure
- **Nethereum.Geth** - Geth-specific APIs
- **Nethereum.Parity** - OpenEthereum/Parity-specific APIs

## Additional Resources

- [Hyperledger Besu Documentation](https://besu.hyperledger.org/)
- [Besu JSON-RPC API](https://besu.hyperledger.org/public-networks/reference/api)
- [Clique Consensus](https://besu.hyperledger.org/private-networks/how-to/configure/consensus/clique)
- [IBFT 2.0 Consensus](https://besu.hyperledger.org/private-networks/how-to/configure/consensus/ibft)
- [EEA Specification](https://entethalliance.org/technical-specifications/)
- [Nethereum Documentation](http://docs.nethereum.com)
