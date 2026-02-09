# Nethereum Account Abstraction (ERC-4337)

This guide shows you how to use Account Abstraction with Nethereum, enabling smart contract wallets to execute transactions through UserOperations instead of traditional EOA transactions.

## What is Account Abstraction?

Account Abstraction (ERC-4337) allows smart contracts to act as user accounts. Instead of sending transactions directly, you create **UserOperations** that are:

1. Signed by your key
2. Sent to a **Bundler** (not directly to the blockchain)
3. Executed by the **EntryPoint** contract via `handleOps`

Benefits include:
- **Gas sponsorship** - Paymasters can pay gas on behalf of users
- **Batched transactions** - Multiple calls in a single operation
- **Custom validation** - Social recovery, multi-sig, session keys
- **Account deployment** - Create accounts on first use

## Quick Start

### 1. Basic Setup

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.Signer;

// Your smart account details
var accountAddress = "0x...";  // Your smart account address
var ownerKey = new EthECKey("your-private-key");

// Bundler service (handles UserOperation submission)
var bundlerService = new AccountAbstractionBundlerService(
    new RpcClient(new Uri("https://your-bundler-url")));

// EntryPoint v0.9 (recommended)
var entryPointAddress = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

// Or use constants
var entryPointAddress = EntryPointAddresses.V09; // 0x433709009B8330FDa32311DF1C2AFA402eD8D009
// var entryPointAddress = EntryPointAddresses.V08; // 0x4337084d9e255ff0702461cf8895ce9e3b5ff108
// var entryPointAddress = EntryPointAddresses.V07; // 0x0000000071727De22E5E9d8BAf0edAc6f37da032
```

### 2. Using with Any Contract Service

The simplest way to use Account Abstraction is to switch an existing contract service to use AA:

```csharp
// Deploy or get your contract service as usual
var myToken = await StandardTokenService.DeployContractAndGetServiceAsync(
    web3, new EIP20Deployment { ... });

// Switch to Account Abstraction - one line!
myToken.ChangeContractHandlerToAA(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

// Now all transactions go through UserOperations
var receipt = await myToken.TransferRequestAndWaitForReceiptAsync(recipient, amount);

// The receipt includes AA-specific information
var aaReceipt = (AATransactionReceipt)receipt;
Console.WriteLine($"UserOp Hash: {aaReceipt.UserOpHash}");
Console.WriteLine($"Success: {aaReceipt.UserOpSuccess}");
```

## Using with Nethereum Standard Contracts

Nethereum provides built-in services for common token standards. All of these now support Account Abstraction.

### ERC20 Tokens

```csharp
// Get the ERC20 service from web3
var erc20 = web3.Eth.ERC20.GetContractService("0xTokenAddress");

// Switch to Account Abstraction
erc20.SwitchToAccountAbstraction(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

// All ERC20 operations now use UserOperations
await erc20.TransferRequestAndWaitForReceiptAsync(recipient, amount);
await erc20.ApproveRequestAndWaitForReceiptAsync(spender, amount);

// Query functions still use normal eth_call (no gas needed)
var balance = await erc20.BalanceOfQueryAsync(accountAddress);
```

### ERC721 NFTs

```csharp
var erc721 = web3.Eth.ERC721.GetContractService("0xNFTAddress");

erc721.SwitchToAccountAbstraction(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

// Transfer NFTs via UserOperation
await erc721.SafeTransferFromRequestAndWaitForReceiptAsync(
    from: accountAddress,
    to: recipient,
    tokenId: 123);
```

### ERC1155 Multi-Tokens

```csharp
var erc1155 = web3.Eth.ERC1155.GetContractService("0xMultiTokenAddress");

erc1155.SwitchToAccountAbstraction(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

await erc1155.SafeTransferFromRequestAndWaitForReceiptAsync(
    from: accountAddress,
    to: recipient,
    id: tokenId,
    amount: quantity,
    data: Array.Empty<byte>());
```

### ENS (Ethereum Name Service)

```csharp
var ensRegistry = new ENSRegistryService(web3.Eth, ensRegistryAddress);

ensRegistry.SwitchToAccountAbstraction(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

// ENS operations via UserOperation
await ensRegistry.SetOwnerRequestAndWaitForReceiptAsync(node, newOwner);
```

## Auto-Deploying Smart Accounts

If your smart account doesn't exist yet, you can have it deployed automatically on the first transaction using a **FactoryConfig**:

```csharp
// Calculate the account address (it doesn't exist yet)
var factory = new SimpleAccountFactoryService(web3, factoryAddress);
var accountAddress = await factory.GetAddressQueryAsync(ownerKey.GetPublicAddress(), salt: 0);

// Fund the address so it can pay for deployment + first transaction
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(accountAddress, 0.1m);

// Configure the factory for auto-deployment
var factoryConfig = new FactoryConfig(
    factoryAddress: factoryAddress,
    owner: ownerKey.GetPublicAddress(),
    salt: 0);

// Switch to AA with factory config
myContract.ChangeContractHandlerToAA(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress,
    factory: factoryConfig);  // <-- Include factory config

// First transaction will:
// 1. Deploy the smart account (via initCode)
// 2. Execute your contract call
var receipt = await myContract.SomeRequestAndWaitForReceiptAsync();
```

The handler automatically checks if the account exists. If not, it includes the `initCode` to deploy it. On subsequent calls, `initCode` is omitted.

## Batching Multiple Calls

Execute multiple contract calls in a single UserOperation. All calls target the handler's contract:

```csharp
var handler = (AAContractHandler)erc20.ContractHandler;

// Use ToBatchCall() extension method for clean, type-safe batching
var receipt = await handler.BatchExecuteAsync(
    new TransferFunction { To = addr1, Value = 100 }.ToBatchCall(),
    new TransferFunction { To = addr2, Value = 200 }.ToBatchCall(),
    new TransferFunction { To = addr3, Value = 300 }.ToBatchCall());

// All three operations succeed or fail atomically
if (receipt.UserOpSuccess)
{
    Console.WriteLine("All transfers completed!");
}
```

### Batch API Options

```csharp
// Recommended: Use ToBatchCall() for type safety
await handler.BatchExecuteAsync(
    new TransferFunction { To = addr1, Value = 100 }.ToBatchCall(),
    new ApproveFunction { Spender = spender, Value = 500 }.ToBatchCall());

// With ETH value: ToBatchCall(ethValue)
await handler.BatchExecuteAsync(
    new DepositFunction().ToBatchCall(ethValue: Web3.Convert.ToWei(1)));

// Simple: Just raw call data bytes
await handler.BatchExecuteAsync(callData1, callData2, callData3);

// Generic: Pass FunctionMessage objects directly (all same type)
await handler.BatchExecuteAsync(
    new CountFunction(),
    new CountFunction(),
    new CountFunction());
```

## Gas Sponsorship with Paymasters

Paymasters can sponsor gas costs for your users:

```csharp
myContract.ChangeContractHandlerToAA(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress)
    .WithPaymaster(paymasterAddress);

// Or with custom paymaster data (e.g., for verifying paymasters)
    .WithPaymaster(paymasterAddress, paymasterData);

// Or with dynamic paymaster data
    .WithPaymaster(new PaymasterConfig(
        paymasterAddress,
        dataProvider: async (userOp) => {
            // Generate signed paymaster data based on the UserOperation
            return await GetSignedPaymasterData(userOp);
        }));
```

## Configuration Options

### Gas and Timeout Settings

```csharp
myContract.ChangeContractHandlerToAA(...)
    .WithGasConfig(new AAGasConfig
    {
        ReceiptPollIntervalMs = 1000,  // How often to check for receipt
        ReceiptTimeoutMs = 60000       // Max wait time for mining
    });
```

### Fluent Configuration

All configuration methods return the handler, allowing chaining:

```csharp
var handler = myContract.ChangeContractHandlerToAA(
        accountAddress, ownerKey, bundlerService, entryPointAddress)
    .WithFactory(factoryConfig)
    .WithPaymaster(paymasterAddress)
    .WithGasConfig(gasConfig);
```

## Understanding the Receipt

The `AATransactionReceipt` extends the standard `TransactionReceipt` with AA-specific fields:

```csharp
var receipt = await myContract.SomeRequestAndWaitForReceiptAsync();
var aaReceipt = (AATransactionReceipt)receipt;

// Standard transaction fields (from the bundle transaction)
Console.WriteLine($"Block: {aaReceipt.BlockNumber}");
Console.WriteLine($"Tx Hash: {aaReceipt.TransactionHash}");

// AA-specific fields
Console.WriteLine($"UserOp Hash: {aaReceipt.UserOpHash}");
Console.WriteLine($"Success: {aaReceipt.UserOpSuccess}");
Console.WriteLine($"Revert Reason: {aaReceipt.RevertReason}");
Console.WriteLine($"Actual Gas Used: {aaReceipt.ActualGasUsed}");
Console.WriteLine($"Actual Gas Cost: {aaReceipt.ActualGasCost}");
Console.WriteLine($"Sender: {aaReceipt.Sender}");
Console.WriteLine($"Paymaster: {aaReceipt.Paymaster}");
```

## Inspecting UserOperations

You can inspect a UserOperation before sending it:

```csharp
var handler = (AAContractHandler)myContract.ContractHandler;

// Create but don't send
var packedOp = await handler.CreateUserOperationAsync(
    new TransferFunction { To = recipient, Value = amount });

Console.WriteLine($"Sender: {packedOp.Sender}");
Console.WriteLine($"Nonce: {packedOp.Nonce}");
Console.WriteLine($"InitCode length: {packedOp.InitCode?.Length ?? 0}");
Console.WriteLine($"CallData: {packedOp.CallData.ToHex()}");
```

## Estimating Gas

```csharp
// Estimate total gas for a UserOperation
var gas = await myContract.ContractHandler.EstimateGasAsync<TransferFunction>(
    new TransferFunction { To = recipient, Value = amount });

Console.WriteLine($"Estimated gas: {gas.Value}");
// This includes: verificationGasLimit + callGasLimit + preVerificationGas
```

## Error Handling

```csharp
try
{
    var receipt = await myContract.TransferRequestAndWaitForReceiptAsync(to, amount);
    var aaReceipt = (AATransactionReceipt)receipt;

    if (!aaReceipt.UserOpSuccess)
    {
        // UserOp was included but inner execution failed
        Console.WriteLine($"Execution failed: {aaReceipt.RevertReason}");
    }
}
catch (TimeoutException ex)
{
    // UserOp wasn't mined within the timeout period
    Console.WriteLine($"Timeout waiting for UserOp: {ex.Message}");
}
catch (RpcClientException ex)
{
    // Bundler rejected the UserOp or connection failed
    Console.WriteLine($"Bundler error: {ex.Message}");
}
```

## Supported Contract Services

The following services implement `IContractHandlerService` and support `SwitchToAccountAbstraction()`:

| Service | Namespace |
|---------|-----------|
| `ERC20ContractService` | `Nethereum.Contracts.Standards.ERC20` |
| `ERC721ContractService` | `Nethereum.Contracts.Standards.ERC721` |
| `ERC1155ContractService` | `Nethereum.Contracts.Standards.ERC1155` |
| `ERC1271ContractService` | `Nethereum.Contracts.Standards.ERC1271` |
| `ERC165SupportsInterfaceContractService` | `Nethereum.Contracts.Standards.ERC165` |
| `EIP3009ContractService` | `Nethereum.Contracts.Standards.EIP3009` |
| `ENSRegistryService` | `Nethereum.Contracts.Standards.ENS` |
| `ETHRegistrarControllerService` | `Nethereum.Contracts.Standards.ENS` |
| `PublicResolverService` | `Nethereum.Contracts.Standards.ENS` |
| `OffchainResolverService` | `Nethereum.Contracts.Standards.ENS` |
| `RegistrarService` | `Nethereum.Contracts.Standards.ENS` |

All **generated contract services** (extending `ContractWeb3ServiceBase`) support `ChangeContractHandlerToAA()`.

## Architecture Overview

```
Your Application
       │
       ▼
┌─────────────────────────┐
│   Contract Service      │  (ERC20Service, your generated services, etc.)
│   with AAContractHandler│
└───────────┬─────────────┘
            │ Creates UserOperation
            ▼
┌─────────────────────────┐
│   Bundler Service       │  (IAccountAbstractionBundlerService)
│   eth_sendUserOperation │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Bundler               │  (Collects UserOps, creates bundle)
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   EntryPoint Contract   │  (handleOps)
│   0x4337090...eD8D009   │  (v0.9)
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Your Smart Account    │  (SimpleAccount, etc.)
│   execute(target, data) │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Target Contract       │  (ERC20, your contract, etc.)
└─────────────────────────┘
```

## Complete Example

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.SimpleAccount;
using Nethereum.Signer;
using Nethereum.Web3;

// Setup
var web3 = new Web3("https://your-rpc-url");
var ownerKey = new EthECKey("your-private-key");
var bundlerService = new AccountAbstractionBundlerService(
    new RpcClient(new Uri("https://your-bundler-url")));

// Deploy factory and get account address
var factory = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
    web3, new SimpleAccountFactoryDeployment { EntryPoint = entryPointAddress });

var accountAddress = await factory.GetAddressQueryAsync(
    ownerKey.GetPublicAddress(), salt: 0);

// Fund the account
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(accountAddress, 0.5m);

// Get an ERC20 service
var usdc = web3.Eth.ERC20.GetContractService("0xUSDCAddress");

// Switch to Account Abstraction with auto-deployment
usdc.SwitchToAccountAbstraction(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress,
    factory: new FactoryConfig(factory.ContractAddress, ownerKey.GetPublicAddress(), 0));

// Transfer USDC via UserOperation
// First call will deploy the account, subsequent calls won't
var receipt = await usdc.TransferRequestAndWaitForReceiptAsync(
    recipient,
    Web3.Convert.ToWei(100, 6)); // 100 USDC (6 decimals)

var aaReceipt = (AATransactionReceipt)receipt;
Console.WriteLine($"Transfer {(aaReceipt.UserOpSuccess ? "succeeded" : "failed")}");
Console.WriteLine($"UserOp Hash: {aaReceipt.UserOpHash}");
Console.WriteLine($"Gas used: {aaReceipt.ActualGasUsed}");
```

## Further Reading

- [ERC-4337 Specification](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com/)
- [Account Abstraction Resources](https://www.erc4337.io/)
