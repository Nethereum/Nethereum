# Getting Started

## What is Nethereum?

Nethereum simplifies Ethereum integration for .NET developers. It supports netstandard and modern .NET versions and works across desktop, mobile, and cloud environments.

## Installation

```bash
# install core package
dotnet add package Nethereum.Web3
```

## First transaction and contract query

```csharp
using Nethereum.Web3;

var web3 = new Web3("https://rpc-url");
var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
```
