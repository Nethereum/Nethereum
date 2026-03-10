---
name: send-eth
description: Send ETH between Ethereum addresses using Nethereum. Use when the user wants to transfer Ether, create a transaction, send crypto, or move funds between wallets. Also triggers for questions about gas, nonce, transaction receipts, EIP-1559 fees, or draining an account balance.
user-invocable: true
---

# Send ETH with Nethereum

NuGet: `Nethereum.Web3`

Two approaches — `EtherTransferService` (handles gas/nonce/signing automatically) or manual transaction signing (full control).

## Quick Start: EtherTransferService

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var privateKey = "0xYOUR_PRIVATE_KEY";
var account = new Account(privateKey, Chain.MainNet);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");

var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipientAddress", 1.5m);
```

Amount is in ETH (not wei) — `1.5m` sends 1.5 ETH.

## Legacy Transfer with Gas Price

```csharp
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(toAddress, 1.11m, gasPriceGwei: 2);
```

`gasPriceGwei` is in Gwei for convenience.

## EIP-1559 Transfer with Fee Suggestion

```csharp
var transferService = web3.Eth.GetEtherTransferService();
var fee = await transferService.SuggestFeeToTransferWholeBalanceInEtherAsync();

var receipt = await transferService
    .TransferEtherAndWaitForReceiptAsync(
        toAddress, 0.1m,
        maxPriorityFee: fee.MaxPriorityFeePerGas.Value,
        maxFeePerGas: fee.MaxFeePerGas.Value);
```

## Estimate Gas Before Sending

```csharp
var transferService = web3.Eth.GetEtherTransferService();
var estimatedGas = await transferService.EstimateGasAsync(toAddress, 1.11m);

var receipt = await transferService
    .TransferEtherAndWaitForReceiptAsync(toAddress, 1.11m, gasPriceGwei: 2, estimatedGas);
```

## Send Entire Balance (EIP-1559)

```csharp
var transferService = web3.Eth.GetEtherTransferService();
var fee = await transferService.SuggestFeeToTransferWholeBalanceInEtherAsync();

var amount = await transferService
    .CalculateTotalAmountToTransferWholeBalanceInEtherAsync(
        fromAddress, maxFeePerGas: fee.MaxFeePerGas.Value);

var receipt = await transferService
    .TransferEtherAndWaitForReceiptAsync(
        destinationAddress, amount,
        fee.MaxPriorityFeePerGas.Value, fee.MaxFeePerGas.Value);
```

## Multi-chain Support

```csharp
var mainnet = new Account(privateKey, Chain.MainNet);    // Chain ID 1
var sepolia = new Account(privateKey, 11155111);          // Sepolia testnet
var polygon = new Account(privateKey, 137);               // Polygon mainnet
// Same address on all chains, only chain ID differs
```

Source: `AccountTypesDocExampleTests.ShouldCreateAccountForDifferentChains`

## Manual Signing: Legacy Transaction

```csharp
var signer = new LegacyTransactionSigner();
var receiverAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
var amount = BigInteger.Parse("1000000000000000000"); // 1 ETH in wei
BigInteger nonce = 0;
BigInteger gasPrice = BigInteger.Parse("20000000000"); // 20 Gwei
BigInteger gasLimit = 21000;

var signedRlpHex = signer.SignTransaction(
    privateKey, receiverAddress, amount, nonce, gasPrice, gasLimit);
```

Source: `SignerDocExampleTests.ShouldSignLegacyTransaction`

## Manual Signing: EIP-1559 Transaction

```csharp
var signer = new Transaction1559Signer();
BigInteger chainId = 1;
BigInteger nonce = 0;
BigInteger maxPriorityFeePerGas = BigInteger.Parse("2000000000");  // 2 Gwei
BigInteger maxFeePerGas = BigInteger.Parse("100000000000");        // 100 Gwei
BigInteger gasLimit = 21000;
var receiverAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
BigInteger amount = BigInteger.Parse("1000000000000000000");       // 1 ETH

var transaction = new Transaction1559(
    chainId, nonce, maxPriorityFeePerGas, maxFeePerGas,
    gasLimit, receiverAddress, amount, null, new List<AccessListItem>());

var signedRlpHex = signer.SignTransaction(privateKey, transaction);
```

Source: `SignerDocExampleTests.ShouldSignEip1559Transaction`

After manual signing, broadcast with:
```csharp
await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + signedRlpHex);
```

## Required Usings

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Model;
using System.Collections.Generic;
using System.Numerics;
```
