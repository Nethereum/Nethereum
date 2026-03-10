---
name: send-eth
description: Send ETH between Ethereum addresses using Nethereum. Use when the user wants to transfer Ether, create a transaction, send crypto, or move funds between wallets. Also triggers for questions about gas, nonce, or transaction receipts.
user-invocable: true
---

# Send ETH with Nethereum

NuGet: `Nethereum.Web3`

## Step 1: Create Account from private key

```csharp
var privateKey = "0xYOUR_PRIVATE_KEY";
var account = new Account(privateKey, Chain.MainNet);
```

## Step 2: Connect to node

```csharp
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
```

## Step 3: Transfer ETH and wait for receipt

```csharp
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipientAddress", 1.5m);
```

## Multi-chain support

```csharp
var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

var mainnet = new Account(privateKey, Chain.MainNet);
var sepolia = new Account(privateKey, 11155111);
var polygon = new Account(privateKey, 137);

// All derive the same address, different chain IDs
// mainnet.Address == sepolia.Address == polygon.Address
```

Source: `AccountTypesDocExampleTests.ShouldCreateAccountForDifferentChains`

## Manual signing: Legacy transaction

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

## Manual signing: EIP-1559 transaction

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

## EIP-155 with chain ID replay protection

```csharp
var tx = new LegacyTransactionChainId(receiverAddress, amount, nonce, gasPrice, gasLimit, chainId);
var signer = new LegacyTransactionSigner();
signer.SignTransaction(privateKey.HexToByteArray(), tx);
```

Source: `SignerDocExampleTests.ShouldSignEip155TransactionWithChainId`

## Required usings

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Signer;
using Nethereum.Model;
using System.Collections.Generic;
using System.Numerics;
```
