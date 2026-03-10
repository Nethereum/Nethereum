---
name: multicall
description: Batch multiple smart contract queries into a single call using Nethereum Multicall (.NET). Use this skill whenever the user asks about batching contract calls, multicall, multiple balances query, batch RPC requests, or aggregating read operations with C# or .NET.
user-invocable: true
---

# Multicall & Batch Queries with Nethereum

NuGet: `Nethereum.Web3`

## Key Namespaces

```csharp
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.QueryHandlers.MultiCall;
```

## Step 1: Define Function and Output DTOs

```csharp
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public string Owner { get; set; }
}

[FunctionOutput]
public class BalanceOfOutputDTO : IFunctionOutputDTO
{
    [Parameter("uint256", "balance", 1)]
    public BigInteger Balance { get; set; }
}
```

## Step 2: Create MulticallInputOutput pairs

Each pair binds a function message (input) with its output DTO and a target contract address:

```csharp
var balanceOfMessage1 = new BalanceOfFunction()
{
    Owner = "0x5d3a536e4d6dbd6114cc1ead35777bab948e3643"
};
var call1 = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    balanceOfMessage1,
    "0x6b175474e89094c44da98b954eedeac495271d0f"); // DAI contract

var balanceOfMessage2 = new BalanceOfFunction()
{
    Owner = "0x6c6bc977e13df9b0de53b251522280bb72383700"
};
var call2 = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    balanceOfMessage2,
    "0x6b175474e89094c44da98b954eedeac495271d0f"); // DAI contract
```

## Step 3a: Execute via Multicall Contract (single eth_call)

Uses the on-chain Multicall3 contract to aggregate calls into one `eth_call`:

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

await web3.Eth.GetMultiQueryHandler().MultiCallAsync(call1, call2);

Console.WriteLine($"Balance 1: {call1.Output.Balance}");
Console.WriteLine($"Balance 2: {call2.Output.Balance}");
```

Source: `MultiCallTest.ShouldCheckBalanceOfMultipleAccounts`

### Custom Multicall address

```csharp
var handler = web3.Eth.GetMultiQueryHandler("0xYourMulticallAddress");
await handler.MultiCallV1Async(call1, call2);
```

## Step 3b: Execute via RPC Batch (no on-chain contract)

Sends individual `eth_call` requests in a single JSON-RPC batch:

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

await web3.Eth.GetMultiQueryBatchRpcHandler().MultiCallAsync(call1, call2);

Console.WriteLine($"Balance 1: {call1.Output.Balance}");
Console.WriteLine($"Balance 2: {call2.Output.Balance}");
```

Source: `MultiCallTest.ShouldCheckBalanceOfMultipleAccountsUsingRpcBatch`

### Manual batch items for advanced scenarios

```csharp
var multiQueryBatchRpcHandler = web3.Eth.GetMultiQueryBatchRpcHandler();

var batchItems = multiQueryBatchRpcHandler
    .CreateMulticallInputOutputRpcBatchItems(0, call1, call2);
```

## Choosing Between Approaches

- **Multicall contract** -- single `eth_call`, guarantees atomic read at same block, requires Multicall3 deployed on the network.
- **RPC batch** -- single HTTP request with multiple `eth_call`, works on any chain without an on-chain contract.

Use Multicall on public networks (Mainnet, Sepolia, Polygon, etc.) where Multicall3 is available. Use RPC batch on private/app chains or when combining with other RPC calls.

## Querying Multiple Different Functions

You can mix different function types in the same batch. Each `MulticallInputOutput` pair is independent:

```csharp
[Function("name", "string")]
public class NameFunction : FunctionMessage { }

[FunctionOutput]
public class NameOutputDTO : IFunctionOutputDTO
{
    [Parameter("string", "", 1)]
    public string Name { get; set; }
}

[Function("totalSupply", "uint256")]
public class TotalSupplyFunction : FunctionMessage { }

[FunctionOutput]
public class TotalSupplyOutputDTO : IFunctionOutputDTO
{
    [Parameter("uint256", "", 1)]
    public BigInteger TotalSupply { get; set; }
}

var nameCall = new MulticallInputOutput<NameFunction, NameOutputDTO>(
    new NameFunction(), daiAddress);
var supplyCall = new MulticallInputOutput<TotalSupplyFunction, TotalSupplyOutputDTO>(
    new TotalSupplyFunction(), daiAddress);
var balanceCall = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    new BalanceOfFunction { Owner = userAddress }, daiAddress);

await web3.Eth.GetMultiQueryHandler().MultiCallAsync(nameCall, supplyCall, balanceCall);

Console.WriteLine($"Token: {nameCall.Output.Name}");
Console.WriteLine($"Supply: {supplyCall.Output.TotalSupply}");
Console.WriteLine($"Balance: {balanceCall.Output.Balance}");
```

## Querying Multiple Contracts

Pass different contract addresses to each `MulticallInputOutput`:

```csharp
var daiBalance = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    new BalanceOfFunction { Owner = userAddress },
    "0x6b175474e89094c44da98b954eedeac495271d0f"); // DAI

var usdcBalance = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    new BalanceOfFunction { Owner = userAddress },
    "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"); // USDC

var wethBalance = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(
    new BalanceOfFunction { Owner = userAddress },
    "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2"); // WETH

await web3.Eth.GetMultiQueryHandler().MultiCallAsync(daiBalance, usdcBalance, wethBalance);
```

## Block Parameter Support

Query at a specific block height:

```csharp
var handler = web3.Eth.GetMultiQueryBatchRpcHandler();
await handler.MultiCallAsync(
    new Nethereum.RPC.Eth.DTOs.BlockParameter(15000000),
    MultiQueryBatchRpcHandler.DEFAULT_CALLS_PER_REQUEST,
    call1, call2);
```

## MulticallInput vs MulticallInputOutput

- **`MulticallInputOutput<TFunction, TOutput>`** -- use when you need decoded output (most common). Results are decoded into `call.Output`.
- **`MulticallInput<TFunction>`** -- use when you only need to send a call but don't need to decode the output.

Both implement `IMulticallInput` and work with both handlers.

## MultiSend (Batched Write Transactions)

Unlike Multicall (read-only), MultiSend batches **write** transactions through a GnosisSafe-style MultiSend contract:

```csharp
using Nethereum.Contracts.TransactionHandlers.MultiSend;

var input1 = new MultiSendFunctionInput<TransferFunction>(
    new TransferFunction { To = recipient1, Value = amount1 }, tokenAddress1);
var input2 = new MultiSendFunctionInput<TransferFunction>(
    new TransferFunction { To = recipient2, Value = amount2 }, tokenAddress2);

var multiSendFunction = new MultiSendFunction(new IMultiSendInput[] { input1, input2 });
```

## Reference

- `MulticallInputOutput<TFunctionInput, TFunctionOutput>` -- pairs a function message with its output DTO and target address
- `MulticallInput<TFunction>` -- input-only wrapper (no output decoding)
- `MultiSendFunction` -- batched write transactions via MultiSend contract
- `MultiSendFunctionInput<T>` -- wraps function message with target and value for MultiSend
- `web3.Eth.GetMultiQueryHandler()` -- returns handler using on-chain Multicall contract
- `web3.Eth.GetMultiQueryBatchRpcHandler()` -- returns handler using JSON-RPC batching
- `MultiQueryBatchRpcHandler.DEFAULT_CALLS_PER_REQUEST` -- default page size (3000)
- `CreateMulticallInputOutputRpcBatchItems()` -- creates batch items for manual composition
