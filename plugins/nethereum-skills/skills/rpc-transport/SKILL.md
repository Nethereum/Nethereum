---
name: rpc-transport
description: Choose and configure JSON-RPC transports in Nethereum. Use when the user asks about HTTP vs WebSocket, IPC connections, SystemTextJson AOT transport, or transport selection.
user-invocable: true
---

# RPC Transport Selection

## Transport Options

| Transport | Package | Streaming | AOT | Use Case |
|-----------|---------|-----------|-----|----------|
| HTTP | `Nethereum.JsonRpc.RpcClient` | No | No | General-purpose |
| SystemTextJson HTTP | `Nethereum.JsonRpc.SystemTextJsonRpcClient` | No | Yes | AOT, trimming, .NET 7+ |
| WebSocket | `Nethereum.JsonRpc.WebSocketStreamingClient` | Yes | No | Subscriptions, real-time |
| IPC | `Nethereum.JsonRpc.IpcClient` | No | No | Local node, lowest latency |

## HTTP (Default)

```csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_KEY");
```

## SystemTextJson HTTP (AOT)

Two clients in `Nethereum.JsonRpc.SystemTextJsonRpcClient`:
- **`SimpleRpcClient`** — zero-config, built-in source-generated JSON context
- **`RpcClient`** — full-featured (custom HttpClient, logging, auth headers)

**Note:** Both this package and the default Newtonsoft package have a class named `RpcClient`. The `using` directive determines which one: `using Nethereum.JsonRpc.SystemTextJsonRpcClient;` for STJ, `using Nethereum.JsonRpc.Client;` for Newtonsoft.

```csharp
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
var client = new SimpleRpcClient("https://eth.drpc.org");
var web3 = new Web3(client);

var balance = await web3.Eth.GetBalance
    .SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");

var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber
    .SendRequestAsync(BlockParameter.CreateLatest());

// ERC20 typed service works with SystemTextJson too
var tokenBalance = await web3.Eth.ERC20
    .GetContractService("0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2")
    .BalanceOfQueryAsync("0x8ee7d9235e01e6b42345120b5d270bdb763624c7");
Console.WriteLine(Web3.Convert.FromWei(tokenBalance, 18));
```

## WebSocket Streaming

```csharp
using Nethereum.JsonRpc.WebSocketStreamingClient;
var client = new StreamingWebSocketClient("wss://...");
await client.StartAsync();
```

## WebSocket for Normal RPC Calls

Use a WebSocket connection for standard (non-subscription) RPC calls:

```csharp
using Nethereum.RPC.Reactive.Eth;

var ethGetBalance = new EthGetBalanceObservableHandler(client);
ethGetBalance.GetResponseAsObservable()
    .Subscribe(balance => Console.WriteLine($"Balance: {balance.Value}"));
await ethGetBalance.SendRequestAsync("0x742d35cc6634c0532925a3b844bc454e4438f44e",
    BlockParameter.CreateLatest());

var ethBlockNumber = new EthBlockNumberObservableHandler(client);
ethBlockNumber.GetResponseAsObservable()
    .Subscribe(block => Console.WriteLine($"Block: {block.Value}"));
await ethBlockNumber.SendRequestAsync();
```

## IPC

```csharp
using Nethereum.JsonRpc.IpcClient;
var client = new IpcClient("/home/user/.ethereum/geth.ipc"); // Linux
var client = new IpcClient(@"\\.\pipe\geth.ipc"); // Windows
var web3 = new Web3(client);
```

## Decision Guide

- **HTTP**: Default choice, works everywhere, hosted providers
- **SystemTextJson**: Required for Native AOT / trimming
- **WebSocket**: Need eth_subscribe (blocks, pending txs, logs)
- **IPC**: Running own node, maximum throughput
