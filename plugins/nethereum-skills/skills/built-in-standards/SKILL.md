---
name: built-in-standards
description: Use Nethereum's built-in typed services for ERC-20, ERC-721, ERC-1155, ENS, ERC-165, ERC-1271, ERC-6492, EIP-3009, and ERC-2535 Diamond. Use this skill whenever the user asks about token standards, NFTs, multi-tokens, interface detection, signature validation, gasless transfers, diamond proxy, ENS resolution, or any standard contract interaction in C#/.NET.
user-invocable: true
---

# Built-in Contract Standards

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

All standard services are accessible via `web3.Eth`:

| Service | Access | Standard |
|---------|--------|----------|
| `ERC20Service` | `web3.Eth.ERC20` | Fungible tokens |
| `ERC721Service` | `web3.Eth.ERC721` | NFTs |
| `ERC1155Service` | `web3.Eth.ERC1155` | Multi-tokens |
| `ERC165SupportsInterfaceService` | `web3.Eth.ERC165` | Interface detection |
| `ERC1271Service` | `web3.Eth.ERC1271` | Contract signature validation |
| `ERC6492Service` | `web3.Eth.ERC6492` | Pre-deploy signature validation |
| `EIP3009Service` | `web3.Eth.EIP3009` | Gasless transfers (USDC) |
| `Permit2Service` | `web3.Eth.GetPermit2Service()` | Uniswap Permit2 (canonical) |
| `ERC2535DiamondService` | `web3.Eth.ERC2535Diamond` | Diamond proxy |
| `ENSService` | `web3.Eth.GetEnsService()` | Name resolution (ENSIP-23 Universal Resolver by default) |
| `ENSUniversalResolverService` | `web3.Eth.GetEnsUniversalResolverService()` | Batched records + CCIP-Read |

## ERC-20: Fungible Tokens

```csharp
var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);

var name = await erc20.NameQueryAsync();
var symbol = await erc20.SymbolQueryAsync();
var decimals = await erc20.DecimalsQueryAsync();
var totalSupply = await erc20.TotalSupplyQueryAsync();
var balance = await erc20.BalanceOfQueryAsync(ownerAddress);
var allowance = await erc20.AllowanceQueryAsync(owner, spender);

// Transfer
var receipt = await erc20.TransferRequestAndWaitForReceiptAsync(to, amount);
```

## ERC-721: NFTs

```csharp
var erc721 = web3.Eth.ERC721.GetContractService(nftAddress);

var name = await erc721.NameQueryAsync();
var symbol = await erc721.SymbolQueryAsync();
var balance = await erc721.BalanceOfQueryAsync(ownerAddress);
var owner = await erc721.OwnerOfQueryAsync(tokenId);
var tokenUri = await erc721.TokenURIQueryAsync(tokenId);
```

## ERC-1155: Multi-Tokens

```csharp
var erc1155 = web3.Eth.ERC1155.GetContractService(contractAddress);

var balance = await erc1155.BalanceOfQueryAsync(owner, tokenId);
var balances = await erc1155.BalanceOfBatchQueryAsync(owners, tokenIds);
var uri = await erc1155.UriQueryAsync(tokenId);
```

## ERC-165: Interface Detection

```csharp
var erc165 = web3.Eth.ERC165.GetContractService(contractAddress);

var supportsErc721 = await erc165
    .SupportsInterfaceQueryAsync("0x80ac58cd"); // ERC-721 interface ID

var supportsErc1155 = await erc165
    .SupportsInterfaceQueryAsync("0xd9b67a26"); // ERC-1155 interface ID

// Common interface IDs: ERC-165=0x01ffc9a7, ERC-721=0x80ac58cd, ERC-1155=0xd9b67a26
```

## ERC-1271: Contract Signature Validation

```csharp
var erc1271 = web3.Eth.ERC1271.GetContractService(contractAddress);

var isValid = await erc1271
    .IsValidSignatureAndValidateReturnQueryAsync(messageHash, signature);
```

## ERC-6492: Pre-Deploy Signature Validation

```csharp
var isValid = await web3.Eth.ERC6492
    .IsValidSignatureQueryAsync(contractAddress, messageHash, signature);
```

## EIP-3009: Gasless Transfer With Authorization

Low-level:
```csharp
var eip3009 = web3.Eth.EIP3009.GetContractService(usdcAddress);
var receipt = await eip3009.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
    from, to, value, validAfter, validBefore, nonce, v, r, s);
```

X402 higher-level (`Nethereum.X402`):
```csharp
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

// Build authorization + sign with EIP-712
var builder = new TransferWithAuthorisationBuilder();
var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);
var signer = new TransferWithAuthorisationSigner();
var signature = await signer.SignWithPrivateKeyAsync(
    authorization, "USDC", "2", chainId, usdcAddress, privateKey);

// Verify + settle (x402 v2: only an RPC endpoint per chain id is configured)
var clients = new Dictionary<int, IClient> { [84532] = new RpcClient(new Uri(rpcUrl)) };
var service = new X402TransferWithAuthorisation3009Service(facilitatorKey, clients);
var result = await service.VerifyPaymentAsync(paymentPayload, requirements);
var settlement = await service.SettlePaymentAsync(paymentPayload, requirements);

// ReceiveWithAuthorization (receiver pays gas)
var receiveService = new X402ReceiveWithAuthorisation3009Service(receiverKey, clients);
```

## Permit2: Uniswap Token Approvals

The canonical [Permit2](https://github.com/Uniswap/permit2) contract (same address on every EVM chain) is a first-class service via `web3.Eth.GetPermit2Service()` (defaults to `CommonAddresses.PERMIT2_ADDRESS`). It covers AllowanceTransfer (`approve`/`permit`/`transferFrom`) and SignatureTransfer (`permitTransferFrom`/`permitWitnessTransferFrom`) plus nonce management.

```csharp
using Nethereum.Contracts.Standards.Permit2;

var permit2 = web3.Eth.GetPermit2Service();

// Reads
var domainSeparator = await permit2.DomainSeparatorQueryAsync();
var allowance = await permit2.AllowanceQueryAsync(owner, token, spender);   // (amount, expiration, nonce)

// SignatureTransfer: the owner's signature is produced with Nethereum.Signer.EIP712's PermitSigner.
// The on-chain `permit` tuple omits spender (Permit2 uses msg.sender); the signed message includes it.
var receipt = await permit2.PermitTransferFromRequestAndWaitForReceiptAsync(
    permit,                                                    // PermitTransferFrom { permitted, nonce, deadline }
    new SignatureTransferDetails { To = recipient, RequestedAmount = amount },
    owner,
    signature);
```

Sign SignatureTransfer messages with `PermitSigner` (from `Nethereum.Signer.EIP712`), which includes the `spender` the on-chain tuple omits:

```csharp
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Signer.EIP712.Permit2;

var signature = PermitSigner.SignPermitTransferFrom(
    chainId, CommonAddresses.PERMIT2_ADDRESS,
    new PermitTransferFromWithSpender
    {
        Permitted = new TokenPermissions { Token = token, Amount = amount },
        Spender = spender,   // the caller; hashed as msg.sender on-chain
        Nonce = nonce,
        Deadline = deadline
    },
    key);
```

Layering: signing types in `Nethereum.ABI.EIP712.Permit2`, contract tuples in `Nethereum.Contracts.Standards.Permit2`, signer in `Nethereum.Signer.EIP712`. For x402 payments over Permit2 (witness variant), see the `x402-payments` skill.

## ENS: Name Resolution

Resolution defaults to the ENSIP-23 Universal Resolver, with automatic EIP-3668
CCIP-Read for offchain and wildcard (ENSIP-10) names.

```csharp
var ensService = web3.Eth.GetEnsService();
var address = await ensService.ResolveAddressAsync("vitalik.eth");
var name = await ensService.ReverseResolveAsync(address);
var url = await ensService.ResolveTextAsync("vitalik.eth", TextDataKey.url);
```

Batch a name's records (address + content hash + text records) in one round-trip,
or resolve many names at once, via the Universal Resolver directly:

```csharp
var universalResolver = web3.Eth.GetEnsUniversalResolverService();
var record = await universalResolver.ResolveRecordAsync("nick.eth", TextDataKey.url, TextDataKey.avatar);
var records = await universalResolver.ResolveRecordsAsync(new[] { "nick.eth", "nethereum.eth" }, TextDataKey.url);
```

## Historical Queries

All query methods accept `BlockParameter`:

```csharp
var blockParam = new Nethereum.RPC.Eth.DTOs.BlockParameter(15000000);
var historicalBalance = await erc20.BalanceOfQueryAsync(address, blockParam);
```

## Code-Generated Contract Services (ContractServiceBase)

```csharp
// Introspect all registered types in a code-generated service
var functionTypes = myContractService.GetAllFunctionTypes();
var eventTypes = myContractService.GetAllEventTypes();
var errorTypes = myContractService.GetAllErrorTypes();

// Get ABI metadata and Keccak signatures
var functionABIs = myContractService.GetAllFunctionABIs();
var eventABIs = myContractService.GetAllEventABIs();
var errorABIs = myContractService.GetAllErrorABIs();
var functionSigs = myContractService.GetAllFunctionSignatures();
var eventSigs = myContractService.GetAllEventsSignatures();
var errorSigs = myContractService.GetAllErrorsSignatures();
```

## Low-Level Storage Access (StorageUtil)

```csharp
using Nethereum.Contracts.ContractStorage;

// Calculate the storage key for mapping(address => ...) at slot 0
var storageKey = StorageUtil.CalculateMappingAddressStorageKey(ownerAddress, 0);

// Read the raw storage value
var storageValue = await web3.Eth.GetStorageAt
    .SendRequestAsync(contractAddress, storageKey);
```

## Key Rules

- **NEVER write raw ABI JSON** for standard contracts -- always use typed services
- **ALWAYS check** `Nethereum.Contracts.Standards/` before any contract interaction
- All services accept optional `BlockParameter` for historical queries
- For batch operations use `MultiQueryBatchRpcHandler` with `CreateMulticallInputOutputRpcBatchItems`
