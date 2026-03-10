---
name: eip7702
description: Delegate smart contract code to EOAs with EIP-7702 using Nethereum (.NET). Use when the user asks about EOA delegation, EIP-7702, Type 4 transactions, SetCode transactions, delegating smart account logic to an EOA, sponsored delegation, batch authorization, smart account upgrades for EOAs, or combining EIP-7702 with ERC-4337 account abstraction.
user-invocable: true
---

# EIP-7702: EOA Code Delegation

NuGet: `Nethereum.Web3`, `Nethereum.Accounts`

EIP-7702 lets an EOA temporarily delegate its execution to a smart contract. Calls to the EOA run the delegate contract's code in the EOA's context (same address, balance, storage).

## High-Level: Self-Delegation

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var account = new Account("0xYOUR_PRIVATE_KEY", Chain.MainNet);
var web3 = new Web3(account, "https://mainnet.infura.io/v3/YOUR_KEY");

var authorisationService = web3.Eth.GetEIP7022AuthorisationService();

// Delegate your EOA to a smart contract
var receipt = await authorisationService
    .AuthoriseRequestAndWaitForReceiptAsync("0xDelegateContractAddress");

// Universal authorization (chainId=0, valid on any chain)
var receipt = await authorisationService
    .AuthoriseRequestAndWaitForReceiptAsync("0xDelegateContract", useUniversalZeroChainId: true);
```

## Query Delegation State

```csharp
bool isDelegated = await authorisationService.IsDelegatedAccountAsync(address);
string delegateAddress = await authorisationService.GetDelegatedAccountAddressAsync(address);
```

Checks if account code starts with `0xef0100` (delegation prefix).

## Remove Delegation

```csharp
var receipt = await authorisationService
    .RemoveAuthorisationRequestAndWaitForReceiptAsync();
```

## Sponsored Delegation

Sponsor pays gas, sponsored account signs only the authorization:

```csharp
using Nethereum.Accounts;
using Nethereum.Signer;

var sponsor = new Account("0xSPONSOR_KEY", Chain.MainNet);
var web3 = new Web3(sponsor, "https://mainnet.infura.io/v3/YOUR_KEY");

var sponsoredKey = new EthECKey("0xSPONSORED_PRIVATE_KEY");

var sponsorService = new EIP7022SponsorAuthorisationService(
    web3.TransactionManager, web3.Eth);

var receipt = await sponsorService
    .AuthoriseSponsoredRequestAndWaitForReceiptAsync(
        sponsoredKey,
        "0xDelegateContractAddress",
        useUniversalZeroChainId: true,
        brandNewAccount: true);
```

### Batch Sponsorship

```csharp
var keys = new EthECKey[] { key1, key2, key3 };
var receipt = await sponsorService
    .AuthoriseBatchSponsoredRequestAndWaitForReceiptAsync(
        keys, "0xDelegateContract", useUniversalZeroChainId: true, brandNewAccount: true);
```

## Low-Level: Manual Authorization

```csharp
using Nethereum.Model;
using Nethereum.Signer;

// 1. Create authorization
var authorisation = new Authorisation7702
{
    ChainId = 1,                          // 0 for universal
    Address = "0xDelegateContractAddress",
    Nonce = 0                              // must match EOA's current nonce
};

// 2. Sign authorization
var ecKey = new EthECKey("0xYOUR_PRIVATE_KEY");
var authSigner = new Authorisation7702Signer();
var signedAuth = authSigner.SignAuthorisation(ecKey, authorisation);

// 3. Build Type 4 transaction
var tx = new Transaction7702(
    chainId: 1, nonce: 0,
    maxPriorityFeePerGas: BigInteger.Parse("2000000000"),
    maxFeePerGas: BigInteger.Parse("100000000000"),
    gasLimit: 100000,
    receiverAddress: ecKey.GetPublicAddress(),
    amount: BigInteger.Zero, data: null,
    accessList: new List<AccessListItem>(),
    authorisationList: new List<Authorisation7702Signed> { signedAuth });

// 4. Sign and broadcast
var txSigner = new Transaction7702Signer();
var signedTxHex = txSigner.SignTransaction(ecKey, tx);
await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + signedTxHex);
```

## Inline Authorization (with Contract Calls)

Attach authorization to the same transaction that calls the delegate:

```csharp
var executeFunction = new ExecuteFunction
{
    Calls = batchCalls,
    Gas = 1000000,  // must set gas manually
    AuthorisationList = new List<Authorisation>
    {
        new Authorisation { Address = "0xDelegateContractAddress" }
    }
};
var receipt = await contractService.ExecuteRequestAndWaitForReceiptAsync(executeFunction);
```

Unsigned `Authorisation` is signed automatically by the transaction manager.

## Recover Signer from Authorization

```csharp
var signerAddress = EthECKeyBuilderFromSignedAuthorisation.RecoverSignerAddress(signedAuth);
```

## Gas Costs

- Per authorization base: 12,500 gas
- Per account (new or existing): 25,000 gas

```csharp
int extraGas = AuthorisationGasCalculator.CalculateGasForAuthorisationDelegation(
    numberOfNew: 3, numberOfExisting: 1);
```

## Key Facts

- Type byte: `0x04`
- Delegation prefix in account code: `0xef0100` + 20-byte address
- Uses EIP-1559 fee model (`maxFeePerGas` + `maxPriorityFeePerGas`)
- Delegation persists across transactions until explicitly removed
- No delegation chaining (only first level resolved)
- Hardware wallets (Ledger, Trezor) and cloud KMS (AWS, Azure) all support `SignAsync(Transaction7702)`

## Required Usings

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Accounts;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using System.Collections.Generic;
using System.Numerics;
```
