---
name: account-abstraction
description: "Help users implement ERC-4337 Account Abstraction with Nethereum — send UserOperations, use smart accounts, route contract calls through a bundler, enable gasless transactions, or work with AA in .NET/C#. Use this skill whenever the user mentions account abstraction, ERC-4337, UserOperations, smart accounts, bundler integration, gasless UX, or anything involving AA on EVM chains with Nethereum."
user-invocable: true
---

# Account Abstraction (ERC-4337)

ERC-4337 replaces EOA transactions with **UserOperations** — signed intents executed by smart contract wallets through a Bundler and EntryPoint contract. Use Account Abstraction when you need: batched calls, gas sponsorship (paymasters), social recovery, session keys, or modular validation logic.

## When to Use This

- User wants to send transactions from a **smart contract wallet** instead of an EOA
- User needs **gasless transactions** (paymaster sponsors gas)
- User wants to **batch multiple calls** into one atomic operation
- User is building on ERC-4337 infrastructure
- User mentions UserOperations, bundlers, EntryPoint, or smart accounts

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
dotnet add package Nethereum.AccountAbstraction.SimpleAccount  # for SimpleAccount factory
```

## The Simple Way: AAContractHandler

Switch any existing typed contract service to route through Account Abstraction. Every call becomes a UserOperation automatically:

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var web3 = new Web3(new Account(privateKey), rpcUrl);
var erc20Service = new StandardTokenService(web3, tokenAddress);

// One line: switch to AA
erc20Service.ChangeContractHandlerToAA(
    accountAddress,    // smart account address
    privateKey,        // signer key
    bundlerUrl,        // bundler JSON-RPC endpoint
    entryPointAddress  // EntryPoint contract address
);

// Use the service exactly as before — AA is transparent
var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
    new TransferFunction { To = recipient, Value = amount });

// Check AA-specific fields
Console.WriteLine($"UserOp Hash: {receipt.UserOpHash}");
Console.WriteLine($"UserOp Success: {receipt.UserOpSuccess}");
```

### With Factory for First-Time Deployment

If the smart account hasn't been deployed yet:

```csharp
erc20Service.ChangeContractHandlerToAA(
    accountAddress, privateKey, bundlerUrl, entryPointAddress,
    new FactoryConfig
    {
        FactoryAddress = factoryAddress,
        Owner = ownerAddress,
        Salt = 0
    });
```

### With Explicit Key and Bundler Service

For full control, pass an `EthECKey` and `IAccountAbstractionBundlerService`:

```csharp
var signerKey = new EthECKey(privateKey);
var bundlerService = new AccountAbstractionBundlerService(
    new RpcClient(new Uri(bundlerUrl)));

var handler = erc20Service.ChangeContractHandlerToAA(
    accountAddress, signerKey, bundlerService, entryPointAddress);

// Configure gas, paymaster, etc.
handler.WithPaymaster(paymasterAddress);
handler.WithGasConfig(new AAGasConfig
{
    CallGasMultiplier = 1.2m,
    VerificationGasMultiplier = 1.1m
});
```

## Manual UserOperation Flow

When you need full control over UserOperation construction:

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.SimpleAccount;
using Nethereum.Signer;

// 1. Build the inner call
var executeFunction = new ExecuteFunction
{
    Target = recipientAddress,
    Value = Web3.Convert.ToWei(0.01m),
    Data = new byte[0]
};

// 2. Create UserOperation
var userOp = new UserOperation
{
    Sender = smartAccountAddress,
    CallData = executeFunction.GetCallData(),
    MaxFeePerGas = Web3.Convert.ToWei(30, Nethereum.Util.UnitConversion.EthUnit.Gwei),
    MaxPriorityFeePerGas = Web3.Convert.ToWei(1, Nethereum.Util.UnitConversion.EthUnit.Gwei)
};

// 3. Estimate gas via bundler
var bundlerClient = new Nethereum.JsonRpc.Client.RpcClient(new Uri(bundlerUrl));
var bundlerService = new AccountAbstractionBundlerService(bundlerClient);

var gasEstimate = await bundlerService.EstimateUserOperationGas
    .SendRequestAsync(userOp, entryPointAddress);

userOp.CallGasLimit = gasEstimate.CallGasLimit?.Value;
userOp.VerificationGasLimit = gasEstimate.VerificationGasLimit?.Value;
userOp.PreVerificationGas = gasEstimate.PreVerificationGas?.Value;

// 4. Sign
var entryPointService = new EntryPointService(web3, entryPointAddress);
var key = new EthECKey(privateKey);
var packedUserOp = await entryPointService.SignAndInitialiseUserOperationAsync(userOp, key);

// 5. Send to bundler
var userOpHash = await bundlerService.SendUserOperation
    .SendRequestAsync(userOp, entryPointAddress);
```

## EntryPoint Versions

```csharp
EntryPointAddresses.V06    // Original ERC-4337
EntryPointAddresses.V07    // Packed format
EntryPointAddresses.V08    // Incremental improvements
EntryPointAddresses.V09    // 0x433709009B8330FDa32311DF1C2AFA402eD8D009
EntryPointAddresses.Latest // Alias for V09 — recommended
```

## Key Types

- **`UserOperation`** — UserOp fields (Sender, CallData as byte[], gas fields as BigInteger?)
- **`AAContractHandler`** — routes contract service calls through AA automatically
- **`AATransactionReceipt`** — extends TransactionReceipt with UserOpHash, UserOpSuccess, ActualGasCost, Paymaster, Sender
- **`FactoryConfig`** — factory address + owner + salt for first-time account deployment
- **`PaymasterConfig`** — paymaster address + optional data provider callback
- **`AAGasConfig`** — gas buffers, multipliers, receipt polling config
- **`AccountAbstractionBundlerService`** — constructor takes `IClient`, NOT a URL string
- **`EntryPointService`** — hash and sign UserOperations
- **`BatchCall`** — for batching multiple calls in one UserOperation

## Decision Guide

| Scenario | Approach |
|----------|----------|
| Existing contract service + AA | `ChangeContractHandlerToAA` (simple path) |
| Full control over UserOp fields | Manual UserOperation construction |
| First-time account deployment | Add `FactoryConfig` to handler or set `InitCode` on UserOp |
| Gasless transactions | Add `PaymasterConfig` via `handler.WithPaymaster()` |
| Multiple calls in one tx | `handler.BatchExecuteAsync()` with `ToBatchCall()` |

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/overview
