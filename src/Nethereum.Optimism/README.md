# Nethereum.Optimism

Contract services for Optimism L2 bridges and cross-domain messaging. Provides typed interfaces for depositing/withdrawing ETH and ERC20 tokens between Ethereum (L1) and Optimism (L2).

## Overview

Nethereum.Optimism provides typed contract services for Optimism's L1 and L2 smart contracts. Use these services to interact with the Standard Bridge for token deposits/withdrawals and Cross Domain Messengers for arbitrary message passing between layers.

**Contract Services:**
- **L1StandardBridge** - L1 contract for depositing ETH and ERC20 to L2
- **L2StandardBridge** - L2 contract for withdrawing tokens back to L1
- **L1CrossDomainMessenger** - L1 contract for cross-layer messaging
- **L2CrossDomainMessenger** - L2 contract for cross-layer messaging
- **L2StandardERC20** - L2 ERC20 token paired with L1 token
- **L2StandardTokenFactory** - Factory for creating L2 standard tokens
- **CrossMessagingWatcherService** - Watch for cross-layer message relay events

**Pre-deployed Addresses:**
Optimism deploys standard contracts at fixed L2 addresses.

## Installation

```bash
dotnet add package Nethereum.Optimism
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Optimism
```

## Dependencies

**Package References:**
- Nethereum.Web3

## Pre-deployed Contract Addresses

Optimism L2 contracts are deployed at fixed addresses:

```csharp
using Nethereum.Optimism;

// L2 pre-deployed addresses (same on all Optimism networks)
var l2Bridge = PredeployedAddresses.L2StandardBridge;                    // 0x4200000000000000000000000000000000000010
var l2Messenger = PredeployedAddresses.L2CrossDomainMessenger;           // 0x4200000000000000000000000000000000000007
var l2TokenFactory = PredeployedAddresses.L2StandardTokenFactory;        // 0x4200000000000000000000000000000000000012
var sequencerFeeVault = PredeployedAddresses.OVM_SequencerFeeVault;      // 0x4200000000000000000000000000000000000011
var l2ToL1MessagePasser = PredeployedAddresses.OVM_L2ToL1MessagePasser; // 0x4200000000000000000000000000000000000000
var gasPriceOracle = PredeployedAddresses.OVM_GasPriceOracle;            // 0x420000000000000000000000000000000000000F
```

**From:** `src/Nethereum.Optimism/PredeployedAddresses.cs:7`

## Bridging ETH and ERC20 Tokens

### Depositing ERC20 from L1 to L2

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Optimism.L1StandardBridge;
using Nethereum.Optimism.L1StandardBridge.ContractDefinition;
using Nethereum.Optimism.L2StandardERC20;
using Nethereum.Optimism.L2StandardERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

// Initialize Web3 for both layers
var account = new Account("PRIVATE_KEY");
var web3L1 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var web3L2 = new Web3(account, "https://mainnet.optimism.io");

// Get L1 bridge address from address manager or known deployment
var l1BridgeAddress = "0x..."; // L1StandardBridge contract address
var l1StandardBridge = new L1StandardBridgeService(web3L1, l1BridgeAddress);

// Approve L1 token for bridge
var l1TokenAddress = "0x...";
var tokenService = web3L1.Eth.ERC20.GetContractService(l1TokenAddress);
await tokenService.ApproveRequestAndWaitForReceiptAsync(l1BridgeAddress, 1_000_000);

// Deposit ERC20 to L2
var l2TokenAddress = "0x..."; // L2 token address (paired with L1 token)
var depositReceipt = await l1StandardBridge.DepositERC20RequestAndWaitForReceiptAsync(
    new DepositERC20Function
    {
        L1Token = l1TokenAddress,
        L2Token = l2TokenAddress,
        Amount = 1_000_000,
        L2Gas = 200_000,
        Data = "0x".HexToByteArray()
    });

// Wait for message to be relayed to L2 (see Cross-Layer Message Watching)
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:82`

### Withdrawing ERC20 from L2 to L1

```csharp
using Nethereum.Optimism.L2StandardBridge;
using Nethereum.Optimism.L2StandardBridge.ContractDefinition;
using Nethereum.Optimism;

// L2 bridge is at pre-deployed address
var l2StandardBridge = new L2StandardBridgeService(web3L2, PredeployedAddresses.L2StandardBridge);

// Withdraw tokens back to L1
var withdrawReceipt = await l2StandardBridge.WithdrawRequestAndWaitForReceiptAsync(
    new WithdrawFunction
    {
        L2Token = l2TokenAddress,
        Amount = 1_000_000,
        L1Gas = 200_000,
        Data = "0x".HexToByteArray()
    });

// Withdrawal must be finalized on L1 after challenge period (7 days on mainnet)
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:101`

## Cross-Layer Message Watching

Watch for cross-layer messages and track their relay status.

### Get Message Hashes from Transaction

```csharp
using Nethereum.Optimism;

var watcher = new CrossMessagingWatcherService();

// Get message hashes from L1 deposit transaction
var messageHashes = watcher.GetMessageHashes(depositReceipt);
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:93`

**From:** `src/Nethereum.Optimism/CrossMessagingWatcherService.cs:40`

### Wait for Message Relay

Wait for a message to be relayed to the destination layer:

```csharp
using System.Linq;

var l2MessengerAddress = PredeployedAddresses.L2CrossDomainMessenger;

// Wait for message to be relayed on L2
var relayReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(
    web3L2,
    l2MessengerAddress,
    messageHashes.First());
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:95`

**From:** `src/Nethereum.Optimism/CrossMessagingWatcherService.cs:60`

## Deploying L2 Standard ERC20

Create an L2 token paired with an existing L1 token:

```csharp
using Nethereum.Optimism.L2StandardERC20;
using Nethereum.Optimism.L2StandardERC20.ContractDefinition;
using Nethereum.Optimism;

var l2TokenDeployment = new L2StandardERC20Deployment
{
    L1Token = l1TokenAddress,
    L2Bridge = PredeployedAddresses.L2StandardBridge,
    Name = "My Token",
    Symbol = "MTK"
};

var l2TokenReceipt = await L2StandardERC20Service.DeployContractAndWaitForReceiptAsync(
    web3L2,
    l2TokenDeployment);

var l2TokenAddress = l2TokenReceipt.ContractAddress;
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:64`

## Address Manager

Get L1 contract addresses from Optimism's address manager:

```csharp
using Nethereum.Optimism.Lib_AddressManager;
using Nethereum.Optimism;

var addressManagerAddress = "0x..."; // Optimism address manager contract
var addressManager = new Lib_AddressManagerService(web3L1, addressManagerAddress);

// Get L1 contract addresses
var l1BridgeAddress = await addressManager.GetAddressQueryAsync(StandardAddressManagerKeys.L1StandardBridge);
var l1MessengerAddress = await addressManager.GetAddressQueryAsync(StandardAddressManagerKeys.L1CrossDomainMessenger);
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:42`

**Standard Keys:**
```csharp
// From StandardAddressManagerKeys class
public const string L1CrossDomainMessenger = "L1CrossDomainMessenger";
public const string L1StandardBridge = "L1StandardBridge";
```

**From:** `src/Nethereum.Optimism/StandardAddressManagerKeys.cs`

## Contract Services

### L1StandardBridgeService

Deposit ETH and ERC20 tokens from L1 to L2.

**Methods:**
- `DepositERC20RequestAsync(l1Token, l2Token, amount, l2Gas, data)` - Deposit ERC20 to L2
- `DepositERC20ToRequestAsync(l1Token, l2Token, to, amount, l2Gas, data)` - Deposit ERC20 to specific address
- `DepositETHRequestAsync(l2Gas, data)` - Deposit ETH to L2
- `DepositETHToRequestAsync(to, l2Gas, data)` - Deposit ETH to specific address

**From:** `src/Nethereum.Optimism/L1StandardBridge/L1StandardBridgeService.cs:45`

### L2StandardBridgeService

Withdraw tokens from L2 back to L1.

**Methods:**
- `WithdrawRequestAsync(l2Token, amount, l1Gas, data)` - Withdraw ERC20 to L1
- `WithdrawToRequestAsync(l2Token, to, amount, l1Gas, data)` - Withdraw ERC20 to specific L1 address
- `WithdrawETHRequestAsync(amount, l1Gas, data)` - Withdraw ETH to L1
- `WithdrawETHToRequestAsync(to, amount, l1Gas, data)` - Withdraw ETH to specific L1 address

**From:** `src/Nethereum.Optimism/L2StandardBridge/L2StandardBridgeService.cs`

### L1CrossDomainMessengerService

Send arbitrary messages from L1 to L2.

**Methods:**
- `SendMessageRequestAsync(target, message, gasLimit)` - Send message to L2 contract
- `RelayMessageRequestAsync(target, sender, message, messageNonce)` - Relay message received from L2

**From:** `src/Nethereum.Optimism/L1CrossDomainMessenger/L1CrossDomainMessengerService.cs`

### L2CrossDomainMessengerService

Send arbitrary messages from L2 to L1.

**Methods:**
- `SendMessageRequestAsync(target, message, gasLimit)` - Send message to L1 contract
- `RelayMessageRequestAsync(target, sender, message, messageNonce, proof)` - Relay message from L1 (requires Merkle proof)

**From:** `src/Nethereum.Optimism/L2CrossDomainMessenger/L2CrossDomainMessengerService.cs`

### L2StandardERC20Service

Interact with L2 ERC20 tokens (standard ERC20 methods plus L2-specific methods).

**L2-Specific Methods:**
- `L1TokenQueryAsync()` - Get paired L1 token address
- `L2BridgeQueryAsync()` - Get L2 bridge address
- `BurnRequestAsync(amount)` - Burn tokens (called by bridge during withdrawal)
- `MintRequestAsync(to, amount)` - Mint tokens (called by bridge during deposit)

**From:** `src/Nethereum.Optimism/L2StandardERC20/L2StandardERC20Service.cs`

### L2StandardTokenFactoryService

Create new L2 standard ERC20 tokens.

**Methods:**
- `CreateStandardL2TokenRequestAsync(l1Token, name, symbol)` - Create L2 token for L1 token

**From:** `src/Nethereum.Optimism/L2StandardTokenFactory/L2StandardTokenFactoryService.cs`

### CrossMessagingWatcherService

Watch for cross-layer message events and track relay status.

**Methods:**
- `GetMessageHashes(receipt)` - Extract message hashes from transaction receipt
- `GetCrossMessageMessageTransactionReceipt(web3, messengerAddress, msgHash, token, numberOfPastBlocks)` - Wait for message relay transaction

**From:** `src/Nethereum.Optimism/CrossMessagingWatcherService.cs:19`

## Complete Example

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Optimism;
using Nethereum.Optimism.L1StandardBridge;
using Nethereum.Optimism.L1StandardBridge.ContractDefinition;
using Nethereum.Optimism.L2StandardBridge;
using Nethereum.Optimism.L2StandardERC20;
using Nethereum.Optimism.L2StandardERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;

var account = new Account("PRIVATE_KEY");
var web3L1 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
var web3L2 = new Web3(account, "https://mainnet.optimism.io");

var watcher = new CrossMessagingWatcherService();

// Get L1 bridge address (from address manager or known deployment)
var l1BridgeAddress = "0x...";
var l1Bridge = new L1StandardBridgeService(web3L1, l1BridgeAddress);

// Create L2 token
var l2TokenDeployment = new L2StandardERC20Deployment
{
    L1Token = l1TokenAddress,
    L2Bridge = PredeployedAddresses.L2StandardBridge,
    Name = "My Token",
    Symbol = "MTK"
};

var l2TokenReceipt = await L2StandardERC20Service.DeployContractAndWaitForReceiptAsync(web3L2, l2TokenDeployment);

// Approve and deposit
var tokenService = web3L1.Eth.ERC20.GetContractService(l1TokenAddress);
await tokenService.ApproveRequestAndWaitForReceiptAsync(l1BridgeAddress, 1_000_000);

var depositReceipt = await l1Bridge.DepositERC20RequestAndWaitForReceiptAsync(
    new DepositERC20Function
    {
        L1Token = l1TokenAddress,
        L2Token = l2TokenReceipt.ContractAddress,
        Amount = 1_000_000,
        L2Gas = 200_000,
        Data = "0x".HexToByteArray()
    });

// Wait for L2 relay
var messageHashes = watcher.GetMessageHashes(depositReceipt);
var relayReceipt = await watcher.GetCrossMessageMessageTransactionReceipt(
    web3L2,
    PredeployedAddresses.L2CrossDomainMessenger,
    messageHashes.First());

// Check L2 balance
var l2TokenService = new L2StandardERC20Service(web3L2, l2TokenReceipt.ContractAddress);
var balance = await l2TokenService.BalanceOfQueryAsync(account.Address);
```

**From:** `tests/Nethereum.Optimism.Testing/ERC20_L1_to_L2_Deposit_and_Withdraw.cs:31`

## Important Notes

- **Withdrawal Challenge Period**: L2 to L1 withdrawals have a 7-day challenge period on mainnet before they can be finalized
- **Gas Limits**: Set appropriate `L2Gas`/`L1Gas` parameters for cross-layer transactions (typical values: 200,000 - 2,000,000)
- **Message Relay**: Deposits (L1→L2) are automatically relayed; withdrawals (L2→L1) require claiming after challenge period
- **L2 Token Deployment**: L2 tokens must be paired with L1 tokens and use the L2StandardBridge as the bridge address

## Related Packages

- **Nethereum.Web3** - Base Web3 implementation
- **Nethereum.Contracts** - Contract interaction infrastructure
- **Nethereum.Contracts.Standards.ERC20** - ERC20 token standard

## Additional Resources

- [Optimism Documentation](https://docs.optimism.io/)
- [Optimism Bridge](https://app.optimism.io/bridge)
- [Standard Bridge Contracts](https://github.com/ethereum-optimism/optimism/tree/develop/packages/contracts-bedrock/src/L1)
- [Nethereum Documentation](http://docs.nethereum.com)
