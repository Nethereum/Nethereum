---
name: query-blocks
description: Query Ethereum blocks, transactions, receipts, balances, and nonces using Nethereum (.NET). Use this skill whenever the user asks about getting block data, reading transactions, checking balances, fetching receipts, getting nonces, querying on-chain state, decoding event logs from receipts, or reading ERC20 token balances with C# or .NET.
user-invocable: true
---

# Query Blocks and Transactions

NuGet: `Nethereum.Web3`

## Connect

```csharp
using Nethereum.Web3;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
```

## Block Number

```csharp
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
```

## Block by Number

```csharp
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
    .SendRequestAsync(new HexBigInteger(blockNumber));
// block.Number, block.Timestamp, block.Transactions[], block.BaseFee
```

## Block by Hash

```csharp
var block = await web3.Eth.Blocks.GetBlockWithTransactionsByHash
    .SendRequestAsync("0xabc123...");
```

## Transaction by Hash

```csharp
var tx = await web3.Eth.Transactions.GetTransactionByHash
    .SendRequestAsync("0xtxhash...");
// tx.From, tx.To, tx.Value, tx.GasPrice
// tx.MaxFeePerGas, tx.MaxPriorityFeePerGas (EIP-1559)
```

## Transaction Receipt

```csharp
var receipt = await web3.Eth.Transactions.GetTransactionReceipt
    .SendRequestAsync("0xtxhash...");
// receipt.Status (1 = success, 0 = revert)
// receipt.GasUsed, receipt.EffectiveGasPrice
// receipt.Logs, receipt.BlockNumber
```

## Account Balance

```csharp
var balance = await web3.Eth.GetBalance.SendRequestAsync("0xaddress...");
var etherAmount = Nethereum.Util.UnitConversion.Convert.FromWei(balance.Value);
```

## Balance at Specific Block

```csharp
var balance = await web3.Eth.GetBalance.SendRequestAsync(
    "0xaddress...",
    new BlockParameter(new HexBigInteger(15000000)));
```

## Transaction Count (Nonce)

```csharp
var nonce = await web3.Eth.Transactions.GetTransactionCount
    .SendRequestAsync("0xaddress...");
```

## Get Code at Address

```csharp
var code = await web3.Eth.GetCode.SendRequestAsync("0xcontract...");
// Returns "0x" for EOAs, bytecode for contracts
```

## Decode Events from Receipt

```csharp
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;

var receipt = await web3.Eth.Transactions.GetTransactionReceipt
    .SendRequestAsync("0x654288d8...");

var events = receipt.DecodeAllEvents<TransferEventDTO>();
Console.WriteLine($"From: {events[0].Event.From}");
Console.WriteLine($"To: {events[0].Event.To}");
Console.WriteLine($"Value: {events[0].Event.Value}");
```

## ERC20 Balance via Typed Service

```csharp
var tokenBalance = await web3.Eth.ERC20
    .GetContractService("0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2")
    .BalanceOfQueryAsync("0x8ee7d9235e01e6b42345120b5d270bdb763624c7");
Console.WriteLine(Web3.Convert.FromWei(tokenBalance, 18));
```
