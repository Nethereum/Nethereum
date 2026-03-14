---
name: aa-batching-paymasters
description: "Help users batch multiple calls into a single UserOperation and sponsor gas with paymasters using Nethereum Account Abstraction. Use when the user mentions batching transactions, atomic multi-call, approve-then-swap in one tx, gas sponsorship, paymasters, gasless transactions, verifying paymaster, deposit paymaster, or PaymasterConfig in .NET/C# with ERC-4337."
user-invocable: true
---

# Batching & Paymasters

Execute multiple contract calls atomically in a single UserOperation, and sponsor gas fees with paymasters so users don't need ETH.

## When to Use This

- User wants to **batch multiple calls** (approve + swap, multi-transfer) in one atomic operation
- User needs **gas sponsorship** — a paymaster pays gas instead of the user
- User is building **gasless UX** for their dApp
- User mentions `BatchExecuteAsync`, `ToBatchCall`, `WithPaymaster`, or `PaymasterConfig`

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
```

## Batch Multiple Calls

### Typed Batch (Same Contract)

```csharp
var transfer1 = new TransferFunction { To = "0xRecipient1", Value = Web3.Convert.ToWei(50) };
var transfer2 = new TransferFunction { To = "0xRecipient2", Value = Web3.Convert.ToWei(25) };

var receipt = await handler.BatchExecuteAsync<TransferFunction>(transfer1, transfer2);
```

### Mixed Batch (Different Contracts)

Use `ToBatchCall()` to convert typed messages:

```csharp
var approve = new ApproveFunction { Spender = dexAddress, Value = Web3.Convert.ToWei(1000) };
var swap = new SwapFunction { AmountIn = Web3.Convert.ToWei(1000) };

var receipt = await handler.BatchExecuteAsync(
    approve.ToBatchCall(),
    swap.ToBatchCall());
```

### Batch with ETH Value

```csharp
var depositCall = new DepositFunction().ToBatchCall(Web3.Convert.ToWei(1));
```

### Raw Calldata Batch

```csharp
var receipt = await handler.BatchExecuteAsync(encodedCallData1, encodedCallData2);
```

## ERC-7579 Batch via SmartAccountService

```csharp
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;

var calls = new[]
{
    new Call { Target = tokenAddress, Value = 0, Data = approveCallData },
    new Call { Target = dexAddress, Value = 0, Data = swapCallData }
};

var receipt = await account.ExecuteBatchAsync(calls);
```

## Paymasters

### Simple Paymaster

```csharp
handler.WithPaymaster(paymasterAddress);
// All subsequent operations use this paymaster for gas
```

### With Static Data

```csharp
handler.WithPaymaster(paymasterAddress, paymasterData);
```

### Verifying Paymaster (Off-Chain Signature)

```csharp
var paymaster = web3.GetVerifyingPaymasterAsync(paymasterAddress, paymasterSignerKey);

handler.WithPaymaster(new PaymasterConfig(paymasterAddress, async userOp =>
{
    return await paymaster.GetPaymasterDataAsync(userOp);
}));
```

### Deposit Paymaster (Pre-Funded)

```csharp
var depositPaymaster = web3.GetDepositPaymasterAsync(paymasterAddress);
handler.WithPaymaster(paymasterAddress);
```

### Dynamic Paymaster Data

```csharp
handler.WithPaymaster(new PaymasterConfig(paymasterAddress, async userOp =>
{
    // Call external paymaster API at submission time
    var response = await httpClient.PostAsJsonAsync("https://paymaster.example.com/sign",
        new { userOp });
    var result = await response.Content.ReadFromJsonAsync<PaymasterResponse>();
    return result.PaymasterData.HexToByteArray();
}));
```

## Decision Guide

| Scenario | Approach |
|----------|----------|
| Same-contract multi-call | `BatchExecuteAsync<T>(msg1, msg2)` |
| Cross-contract atomic ops | `BatchExecuteAsync(msg1.ToBatchCall(), msg2.ToBatchCall())` |
| Raw calldata | `BatchExecuteAsync(bytes1, bytes2)` |
| ERC-7579 modular account | `account.ExecuteBatchAsync(Call[])` |
| Simple gas sponsorship | `handler.WithPaymaster(address)` |
| Per-operation signature | `PaymasterConfig` with async callback |
| Pre-funded sponsorship | Deposit paymaster + static address |

## Common Mistakes

- **Paymaster not funded** — EntryPoint checks paymaster deposit, returns AA31/AA32 errors
- **Paymaster data expired** — verifying paymasters include validity windows; use dynamic data
- **Wrong batch approach** — use `ToBatchCall()` for cross-contract, generic `<T>` for same-contract

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-batching-and-paymasters
