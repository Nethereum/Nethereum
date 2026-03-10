---
name: transaction-models
description: Work with Ethereum transaction types and models in Nethereum. Use when the user needs to create, decode, or inspect transactions (legacy, EIP-1559, EIP-2930, EIP-7702), work with Chain IDs, block headers, access lists, transaction recovery, or TransactionFactory.
user-invocable: true
---

# Transaction Models

NuGet: `Nethereum.Model`, `Nethereum.Signer`

Source: `tests/Nethereum.Signer.UnitTests/ModelDocExampleTests.cs`, `tests/Nethereum.Signer.UnitTests/SignerDocExampleTests.cs`

## Legacy Transaction

```csharp
using System.Numerics;
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

var to = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
var amount = BigInteger.Parse("10000000000000000"); // 0.01 ETH
var nonce = BigInteger.Zero;
var gasPrice = BigInteger.Parse("1000000000000");
var gasLimit = BigInteger.Parse("10000");

var tx = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit);
// tx.TransactionType == TransactionType.LegacyTransaction
```

## EIP-1559 Transaction

```csharp
var tx = new Transaction1559(
    chainId, nonce, maxPriorityFeePerGas, maxFeePerGas,
    gasLimit, receiverAddress, amount, data, accessList: null);
// tx.TransactionType == TransactionType.EIP1559
```

## EIP-2930 Access List Transaction

```csharp
using System.Collections.Generic;

var storageKey = new byte[32];
storageKey[31] = 0x01;
var accessList = new List<AccessListItem>
{
    new AccessListItem(contractAddress, new List<byte[]> { storageKey })
};

var tx = new Transaction2930(chainId, nonce, gasPrice, gasLimit,
    receiverAddress, amount, null, accessList);
// tx.TransactionType == TransactionType.LegacyEIP2930
// Uses gasPrice (like legacy) + access list + chainId
```

## EIP-7702 Authorization Transaction

```csharp
using Nethereum.Signer;

// 1. Create and sign authorization
var authorisation = new Authorisation7702(chainId, contractAddress, nonce);
var authSigner = new Authorisation7702Signer();
var signedAuth = authSigner.SignAuthorisation(ecKey, authorisation);

// 2. Create transaction with authorization list
var tx = new Transaction7702(
    chainId, nonce, maxPriorityFeePerGas, maxFeePerGas,
    gasLimit, receiverAddress, amount, null,
    new List<AccessListItem>(), new List<Authorisation7702Signed> { signedAuth });
// tx.TransactionType == TransactionType.EIP7702
// Uses EIP-1559 fee model + authorization list
```

## Transaction Recovery

```csharp
using Nethereum.Signer;

// Recover sender address from signed transaction RLP
var sender = TransactionVerificationAndRecovery.GetSenderAddress(signedRlpHex);

// Verify a signed transaction is valid
var isValid = TransactionVerificationAndRecovery.VerifyTransaction(signedRlpHex);

// Works with all tx types: Legacy, EIP-1559, EIP-2930, EIP-7702
```

## Decode Transaction from RLP

```csharp
var rlpBytes = rlpHex.HexToByteArray();
var tx = new LegacyTransaction(rlpBytes);
// tx.Nonce, tx.GasPrice, tx.Value, tx.ReceiveAddress, tx.Signature
```

## TransactionFactory (Auto-Detection)

```csharp
var decoded = TransactionFactory.CreateTransaction(signedRlpHex);
// Type prefix: 0x01=EIP-2930, 0x02=EIP-1559, 0x05=EIP-7702, none=Legacy
```

## Chain Enum

| Chain    | ID         | Enum Value        |
|----------|------------|-------------------|
| Mainnet  | 1          | `Chain.MainNet`   |
| Sepolia  | 11155111   | `Chain.Sepolia`   |
| Polygon  | 137        | `Chain.Polygon`   |
| Base     | 8453       | `Chain.Base`      |

## Transaction Type Table

| Type        | Prefix | Enum                              | Fee Model |
|-------------|--------|-----------------------------------|-----------|
| Legacy      | none   | `TransactionType.LegacyTransaction` | gasPrice |
| Access List | 0x01   | `TransactionType.LegacyEIP2930`   | gasPrice + accessList |
| EIP-1559    | 0x02   | `TransactionType.EIP1559`         | maxFeePerGas + maxPriorityFeePerGas |
| Blob        | 0x03   | `TransactionType.Blob`            | EIP-1559 + blobVersionedHashes |
| Set Code    | 0x05   | `TransactionType.EIP7702`         | EIP-1559 + authorisationList |

## Block Header

```csharp
var blockHeader = new BlockHeader
{
    BlockNumber = 1000,
    GasLimit = 8000000,
    GasUsed = 21000,
    BaseFee = 1000000000,
    Coinbase = "0x0000000000000000000000000000000000000000"
};
```

## AccessListItem

```csharp
var item = new AccessListItem(address, new List<byte[]> { storageKey1, storageKey2 });
// item.Address, item.StorageKeys
```
