# Nethereum.StandardNonFungibleTokenERC721

**`ERC721Service`: a typed service for ERC-721 (NFT) contracts, with a deployable reference contract**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.StandardNonFungibleTokenERC721.svg)](https://www.nuget.org/packages/Nethereum.StandardNonFungibleTokenERC721/)

## Overview

This package holds one service class, `ERC721Service`, a wrapper around a `ContractHandler` for the core ERC-721 interface, with its function, event and deployment messages. The package description is "Ethereum Service to interact with ERC721 compliant contracts".

The built-in, current way to work with any ERC-721 contract is `web3.Eth.ERC721` (`Nethereum.Contracts.Standards.ERC721.ERC721Service`), whose `GetContractService(contractAddress)` returns a typed `ERC721ContractService`. Prefer it for new code. Use `Nethereum.StandardNonFungibleTokenERC721` if you want its `ERC721Deployment` reference contract or existing code already uses it. The source does not mark the package obsolete.

The class has no `name`, `symbol`, `tokenURI`, `totalSupply` or enumeration members: it covers `supportsInterface`, `getApproved`, `approve`, `transferFrom`, `safeTransferFrom`, `ownerOf`, `balanceOf`, `setApprovalForAll` and `isApprovedForAll`. The bundled `ERC721Deployment` bytecode has no mint function either, so a freshly deployed contract has no tokens; it is useful for interface and approval testing, not as a token issuer.

## Installation

```bash
dotnet add package Nethereum.StandardNonFungibleTokenERC721
```

Depends on `Nethereum.Web3`.

## ERC721Service

Namespace `Nethereum.StandardNonFungibleTokenERC721`; messages are in `Nethereum.StandardNonFungibleTokenERC721.ContractDefinition`.

```csharp
public partial class ERC721Service
{
    public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3,
        ERC721Deployment eRC721Deployment, CancellationTokenSource cancellationTokenSource = null)
    public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ERC721Deployment eRC721Deployment)
    public static async Task<ERC721Service> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3,
        ERC721Deployment eRC721Deployment, CancellationTokenSource cancellationTokenSource = null)

    public ERC721Service(Nethereum.Web3.Web3 web3, string contractAddress)
    public ContractHandler ContractHandler { get; }

    public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
    public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
    public Task<string> GetApprovedQueryAsync(GetApprovedFunction getApprovedFunction, BlockParameter blockParameter = null)
    public Task<string> GetApprovedQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
    public Task<string> OwnerOfQueryAsync(OwnerOfFunction ownerOfFunction, BlockParameter blockParameter = null)
    public Task<string> OwnerOfQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
    public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
    public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction, BlockParameter blockParameter = null)
    public Task<bool> IsApprovedForAllQueryAsync(string owner, string operatorAddress, BlockParameter blockParameter = null)

    public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
    public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> ApproveRequestAsync(string to, BigInteger tokenId)
    public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)

    public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
    public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> TransferFromRequestAsync(string from, string to, BigInteger tokenId)
    public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)

    public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
    public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger tokenId)
    public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)
    public Task<string> SafeTransferFromRequestAsync(SafeTransferFromWithDataFunction safeTransferFromWithDataFunction)
    public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromWithDataFunction safeTransferFromWithDataFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger tokenId, byte[] data)
    public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, byte[] data, CancellationTokenSource cancellationToken = null)

    public Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
    public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(SetApprovalForAllFunction setApprovalForAllFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> SetApprovalForAllRequestAsync(string to, bool approved)
    public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string to, bool approved, CancellationTokenSource cancellationToken = null)
}
```

### Deploy and query

```csharp
var service = await ERC721Service.DeployContractAndGetServiceAsync(web3,
    new Nethereum.StandardNonFungibleTokenERC721.ContractDefinition.ERC721Deployment());

var supportsErc721 = await service.SupportsInterfaceQueryAsync("0x80ac58cd".HexToByteArray());
var supportsUnknown = await service.SupportsInterfaceQueryAsync("0xffffffff".HexToByteArray());
var balance = await service.BalanceOfQueryAsync(OwnerAddress);
var approvedBefore = await service.IsApprovedForAllQueryAsync(OwnerAddress, OtherAddress);

await service.SetApprovalForAllRequestAndWaitForReceiptAsync(OtherAddress, true);
var approvedAfter = await service.IsApprovedForAllQueryAsync(OwnerAddress, OtherAddress);
```

`0x80ac58cd` is the ERC-721 interface id, so `supportsErc721` is `true` and `supportsUnknown` (`0xffffffff`) is `false`; `balance` is zero because the contract mints nothing; `approvedBefore` is `false` and `approvedAfter` is `true`. Backed by `StandardTokenServicesDevChainTests.ShouldDeployAnErc721ContractAndReadItsInterfaceBalancesAndApprovals` (`DocSection.SmartContracts`, `standard-token-erc721`). This package has no test project of its own, so the test lives in `tests/Nethereum.StandardTokenEIP20.IntegrationTests` (which references this package) and runs on an in-process `DevChainNode`.

## Messages and events

```csharp
public partial class ERC721Deployment : ERC721DeploymentBase
{
    public ERC721Deployment()
    public ERC721Deployment(string byteCode)
}

public class ERC721DeploymentBase : ContractDeploymentMessage
{
    public static string BYTECODE
    public ERC721DeploymentBase()
    public ERC721DeploymentBase(string byteCode)
}
```

`BYTECODE` is the reference contract's bytecode (empty when the package is built with `BYTECODELITE`); the parameterless constructors use it and the `byteCode` constructors deploy your own bytecode with the same message.

Function messages (each a `FunctionMessage`): `SupportsInterfaceFunction`, `GetApprovedFunction`, `ApproveFunction`, `TransferFromFunction`, `SafeTransferFromFunction`, `SafeTransferFromWithDataFunction`, `OwnerOfFunction`, `BalanceOfFunction`, `SetApprovalForAllFunction` and `IsApprovedForAllFunction`. Output DTOs: `SupportsInterfaceOutputDTO`, `GetApprovedOutputDTO`, `OwnerOfOutputDTO`, `BalanceOfOutputDTO`, `IsApprovedForAllOutputDTO`.

Event DTOs (each with a `partial` subclass named `TransferEventDTO`, `ApprovalEventDTO`, `ApprovalForAllEventDTO`):

```csharp
[Event("Transfer")]
public class TransferEventDTOBase : IEventDTO
{
    public virtual string From { get; set; }
    public virtual string To { get; set; }
    public virtual BigInteger TokenId { get; set; }
}

[Event("Approval")]
public class ApprovalEventDTOBase : IEventDTO
{
    public virtual string Owner { get; set; }
    public virtual string Approved { get; set; }
    public virtual BigInteger TokenId { get; set; }
}

[Event("ApprovalForAll")]
public class ApprovalForAllEventDTOBase : IEventDTO
{
    public virtual string Owner { get; set; }
    public virtual string Operator { get; set; }
    public virtual bool Approved { get; set; }
}
```
