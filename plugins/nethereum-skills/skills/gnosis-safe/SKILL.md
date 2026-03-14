---
name: gnosis-safe
description: Execute multi-signature transactions through Gnosis Safe (Safe) using Nethereum (.NET/C#). Use this skill whenever the user asks about multi-sig wallets, Gnosis Safe, Safe transactions, SafeAccount, multi-signature signing, MultiSend, or executing contract calls through a Safe with C# or .NET.
user-invocable: true
---

# Gnosis Safe: Multi-Sig Transactions

Gnosis Safe (now "Safe") is the most widely used multi-signature wallet on Ethereum. Nethereum's `Nethereum.GnosisSafe` package provides three levels of integration: `SafeAccount` (transparent — any contract service auto-routes through Safe), `ChangeContractHandlerToSafeExecTransaction` (per-service switching), and manual transaction building with EIP-712 signatures.

NuGet: `Nethereum.GnosisSafe`

```bash
dotnet add package Nethereum.GnosisSafe
```

## SafeAccount (Recommended)

The simplest approach — create a `SafeAccount` and every contract service created from that `web3` instance automatically executes through the Safe:

```csharp
var safeAccount = new SafeAccount(safeAddress, chainId, privateKey);
var web3 = new Web3(safeAccount, rpcUrl);

// This ERC-20 transfer executes through the Safe
var erc20 = new ERC20ContractService(web3.Eth, tokenAddress);
var receipt = await erc20.TransferRequestAndWaitForReceiptAsync(to, amount);
```

`SafeAccount` implements `IContractServiceConfigurableAccount`, which swaps the contract handler to `SafeExecTransactionContractHandler`. The call data gets packed into a Safe `execTransaction`, signed with EIP-712, and submitted.

## Per-Service Handler Swap

Switch a specific contract service to Safe execution without changing the entire `web3`:

```csharp
var erc20 = new ERC20ContractService(web3.Eth, contractAddress);
erc20.ChangeContractHandlerToSafeExecTransaction(safeAddress, privateKey);
// Only this service goes through Safe; others execute directly
```

This is useful when some operations should go through the Safe and others shouldn't.

## Manual Transaction Building

For full control or multi-owner signature collection:

```csharp
var safeService = new GnosisSafeService(web3, safeAddress);

var transactionData = new EncodeTransactionDataFunction
{
    To = targetAddress,
    Value = 0,
    Data = encodedCallData,
    Operation = 0,  // 0 = Call, 1 = DelegateCall
    SafeTxGas = 0,
    BaseGas = 0,
    SafeGasPrice = 0,
    GasToken = AddressUtil.ZERO_ADDRESS,
    RefundReceiver = AddressUtil.ZERO_ADDRESS
};

var execTx = await safeService.BuildTransactionAsync(
    transactionData, chainId, false, privateKey1, privateKey2);

var receipt = await safeService.ExecTransactionRequestAndWaitForReceiptAsync(execTx);
```

`BuildTransactionAsync` fetches the nonce, hashes the data with EIP-712, signs with each key, orders signatures by signer address (required by Safe), and returns a ready-to-submit function.

## Off-Chain Signature Collection

When owners can't sign in the same session, compute the hash and collect signatures separately:

```csharp
// Compute hash
transactionData.SafeNonce = await safeService.NonceQueryAsync();
var safeHashes = GnosisSafeService.GetSafeHashes(transactionData, chainId, safeAddress);

// Each owner signs independently
var signature = await safeService.SignEncodedTransactionDataAsync(
    transactionData, chainId, convertToSafeVFormat: true);
```

Safe uses a custom V format (V+4 from standard Ethereum). The `convertToSafeVFormat: true` parameter handles this automatically.

### Combine and Execute

```csharp
var signatures = new List<SafeSignature>
{
    new SafeSignature { Address = owner1Address, Signature = signature1 },
    new SafeSignature { Address = owner2Address, Signature = signature2 }
};

var combinedSignatures = safeService.GetCombinedSignaturesInOrder(signatures);
```

Safe requires signatures ordered by signer address (ascending). `GetCombinedSignaturesInOrder` handles sorting and concatenation.

## MultiSend

Batch multiple actions in a single Safe transaction using typed `MultiSendFunctionInput<T>`:

```csharp
var input1 = new MultiSendFunctionInput<TransferFunction>(
    new TransferFunction { To = recipient1, Value = amount1 }, tokenAddress);

var input2 = new MultiSendFunctionInput<TransferFunction>(
    new TransferFunction { To = recipient2, Value = amount2 }, tokenAddress);

var execTx = await safeService.BuildMultiSendTransactionAsync(
    transactionData, chainId, privateKey, false, input1, input2);
var receipt = await safeService.ExecTransactionRequestAndWaitForReceiptAsync(execTx);
```

`MultiSendFunctionInput<T>` implements `IMultiSendInput` and encodes call data automatically. `BuildMultiSendTransactionAsync` sets Operation to DelegateCall (required by MultiSend).

## Query Safe Configuration

```csharp
var owners = await safeService.GetOwnersQueryAsync();
var threshold = await safeService.GetThresholdQueryAsync();
var nonce = await safeService.NonceQueryAsync();
var version = await safeService.VersionQueryAsync();
var isOwner = await safeService.IsOwnerQueryAsync(address);

Console.WriteLine($"Safe v{version}: {owners.Count} owners, threshold {threshold}, nonce {nonce}");
```

## Gas Parameters

| Parameter | Purpose |
|-----------|---------|
| `SafeTxGas` | Gas for the internal Safe transaction. Set 0 to let the Safe estimate. |
| `BaseGas` | Gas overhead independent of execution. Usually 0. |
| `SafeGasPrice` | Gas price for refund calculation. Set 0 for no refund. |
| `GasToken` | Token to pay gas refunds in. `address(0)` = ETH. |
| `RefundReceiver` | Address receiving the gas refund. `address(0)` = tx.origin. |

For most use cases, setting all gas parameters to 0 is correct — the Safe uses Ethereum's standard gas mechanism.

## When to Use Which Approach

| Approach | When |
|----------|------|
| `SafeAccount` | All contract calls should go through Safe — simplest |
| `ChangeContractHandlerToSafeExecTransaction` | Only specific services should use Safe |
| Manual `BuildTransactionAsync` | Need multi-owner signature collection or custom gas params |
| `BuildMultiSendTransactionAsync` | Batch multiple actions in one Safe tx |

For full documentation, see: https://docs.nethereum.com/docs/defi/guide-gnosis-safe
