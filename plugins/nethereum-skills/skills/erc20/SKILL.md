---
name: erc20
description: Query and transfer ERC-20 tokens using Nethereum's built-in typed services (.NET/C#). Use this skill whenever the user asks about ERC-20 tokens, token balances, token transfers, token approvals, allowances, token metadata (name, symbol, decimals), or any fungible token interaction with C# or .NET.
user-invocable: true
---

# ERC-20 Tokens

Nethereum has a built-in typed service for ERC-20 — no ABI needed, no code generation. Just get the service for any token contract address and call methods directly. This is the simplest way to interact with fungible tokens.

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Get the Service

All ERC-20 operations start by getting a typed service for the token's contract address:

```csharp
var erc20 = web3.Eth.ERC20.GetContractService(contractAddress);
```

## Query Token Info

Every ERC-20 token exposes metadata. These are read-only calls — no gas, no signing needed:

```csharp
var name = await erc20.NameQueryAsync();
var symbol = await erc20.SymbolQueryAsync();
var decimals = await erc20.DecimalsQueryAsync();
var totalSupply = await erc20.TotalSupplyQueryAsync();
```

The `decimals` value matters — ERC-20 tokens store balances as integers in the smallest unit. Most tokens use 18 decimals, but stablecoins like USDC use 6. Always use `Web3.Convert.FromWei(value, decimals)` to display human-readable amounts.

## Check Balance

Balances are returned in the token's smallest unit. Use `FromWei` with the token's decimals for a readable value:

```csharp
var balance = await erc20.BalanceOfQueryAsync(myAddress);
Console.WriteLine($"Balance: {Web3.Convert.FromWei(balance, decimals)} {symbol}");
```

## Transfer Tokens

Transfers require a `Web3` instance connected with a funded account. The amount must be in the smallest unit — use `ToWei` with the token's decimals:

```csharp
var receipt = await erc20.TransferRequestAndWaitForReceiptAsync(
    recipientAddress,
    Web3.Convert.ToWei(100, decimals));
```

Gas estimation, nonce, and EIP-1559 fees are handled automatically.

## Approve and TransferFrom

The approve/transferFrom pattern lets another address (like a DEX or smart contract) spend your tokens. First approve a spending limit, then the spender calls `transferFrom`:

```csharp
// Approve the spender
var approveReceipt = await erc20.ApproveRequestAndWaitForReceiptAsync(
    spenderAddress,
    Web3.Convert.ToWei(1000, decimals));

// Check allowance
var allowance = await erc20.AllowanceQueryAsync(myAddress, spenderAddress);
```

Be careful with approvals — approving a large amount gives the spender permission to transfer up to that amount at any time.

## Listen for Transfer Events

ERC-20 transfers emit a `Transfer` event. Nethereum ships a built-in `TransferEventDTO` so you don't need to define your own:

```csharp
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var transferEvent = web3.Eth.GetEvent<TransferEventDTO>(contractAddress);
var filter = transferEvent.CreateFilterInput(
    BlockParameter.CreateEarliest(),
    BlockParameter.CreateLatest());

var transfers = await transferEvent.GetAllChangesAsync(filter);
foreach (var t in transfers)
{
    Console.WriteLine($"{t.Event.From} -> {t.Event.To}: {Web3.Convert.FromWei(t.Event.Value, decimals)}");
}
```

## Historical Queries

All query methods accept an optional `BlockParameter` to read state at a specific block. The node must have archive state for the requested block:

```csharp
var historicalBalance = await erc20.BalanceOfQueryAsync(
    myAddress,
    new BlockParameter(15_000_000));
```

## Available Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `NameQueryAsync()` | `string` | Token name |
| `SymbolQueryAsync()` | `string` | Token symbol |
| `DecimalsQueryAsync()` | `byte` | Decimal places |
| `TotalSupplyQueryAsync()` | `BigInteger` | Total supply in wei |
| `BalanceOfQueryAsync(address)` | `BigInteger` | Balance in wei |
| `AllowanceQueryAsync(owner, spender)` | `BigInteger` | Approved amount |
| `TransferRequestAndWaitForReceiptAsync(to, amount)` | `TransactionReceipt` | Transfer tokens |
| `ApproveRequestAndWaitForReceiptAsync(spender, amount)` | `TransactionReceipt` | Approve spending |
| `TransferFromRequestAndWaitForReceiptAsync(from, to, amount)` | `TransactionReceipt` | Transfer on behalf |

For full documentation, see: https://docs.nethereum.com/docs/smart-contracts/erc20
