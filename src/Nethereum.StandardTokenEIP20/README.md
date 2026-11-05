# Nethereum.StandardTokenEIP20

**`StandardTokenService`: the original typed service for EIP-20 (ERC-20) token contracts, with a deployable reference token**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.StandardTokenEIP20.svg)](https://www.nuget.org/packages/Nethereum.StandardTokenEIP20/)

## Overview

This package holds one service class, `StandardTokenService`, a generated-style wrapper around a `ContractHandler` for an EIP-20 token, together with the function, event and deployment messages it uses (`EIP20Deployment` carries the bytecode of a reference token, so you can deploy a test token). The package description is "Ethereum Service to interact with EIP20 compliant contracts".

The built-in, current way to work with any ERC-20 token is `web3.Eth.ERC20` (`Nethereum.Contracts.Standards.ERC20.ERC20Service`, also reachable as `web3.Eth.StandardTokenERC20`), which creates a typed `ERC20ContractService` for a contract address with `web3.Eth.ERC20.GetContractService(address)` and adds multicall and batch balance helpers. Prefer it for new code that reads or sends tokens. Reach for `Nethereum.StandardTokenEIP20` when you want to deploy the bundled `EIP20Deployment` token, or when existing code already uses `StandardTokenService`. The source does not mark the package or `StandardTokenService` obsolete; only the event classes `Events.DTO.Transfer` and `Events.DTO.Approval` carry `[Obsolete]` ("Please use TransferEventDTO instead" / "Please use ApprovalEventDTO instead").

## Installation

```bash
dotnet add package Nethereum.StandardTokenEIP20
```

Depends on `Nethereum.Web3`.

## StandardTokenService

Namespace `Nethereum.StandardTokenEIP20`.

```csharp
public class StandardTokenService
{
    public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3,
        EIP20Deployment eIP20Deployment, CancellationTokenSource cancellationTokenSource = null)
    public static Task<string> DeployContractAsync(Web3.Web3 web3, EIP20Deployment eIP20Deployment)
    public static async Task<StandardTokenService> DeployContractAndGetServiceAsync(Web3.Web3 web3,
        EIP20Deployment eIP20Deployment, CancellationTokenSource cancellationTokenSource = null)

    public StandardTokenService(Web3.Web3 web3, string address)
    public ContractHandler ContractHandler { get; }

    public Event<ApprovalEventDTO> GetApprovalEvent()
    public Event<TransferEventDTO> GetTransferEvent()

    public Task<string> NameQueryAsync(NameFunction nameFunction = null, BlockParameter blockParameter = null)
    public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction = null, BlockParameter blockParameter = null)
    public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
    public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
    public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
    public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
    public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> BalancesQueryAsync(string address, BlockParameter blockParameter = null)
    public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
    public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
    public Task<BigInteger> AllowedQueryAsync(string owner, string spender, BlockParameter blockParameter = null)

    public Task<string> TransferRequestAsync(TransferFunction transferFunction)
    public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> TransferRequestAsync(string to, BigInteger value)
    public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger value, CancellationTokenSource cancellationToken = null)

    public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
    public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> TransferFromRequestAsync(string from, string to, BigInteger value)
    public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, CancellationTokenSource cancellationToken = null)

    public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
    public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
    public Task<string> ApproveRequestAsync(string spender, BigInteger value)
    public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger value, CancellationTokenSource cancellationToken = null)
}
```

`NameQueryAsync` and `SymbolQueryAsync` read the result with a `StringBytes32Decoder`, so tokens that return `bytes32` names (such as older DAI) decode as well as `string` ones. Note that `BalancesQueryAsync` and `AllowedQueryAsync` call the reference token's public `balances` and `allowed` mappings; use `BalanceOfQueryAsync` and `AllowanceQueryAsync` for any standard token.

### Deploy a token, transfer, approve and read the event

```csharp
var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(web3,
    new EIP20Deployment
    {
        InitialAmount = totalSupply,
        TokenName = "TestToken",
        TokenSymbol = "TST"
    });

var name = await tokenService.NameQueryAsync();
var symbol = await tokenService.SymbolQueryAsync();
var supply = await tokenService.TotalSupplyQueryAsync();
var ownerBalance = await tokenService.BalanceOfQueryAsync(OwnerAddress);

var transferReceipt = await tokenService.TransferRequestAndWaitForReceiptAsync(OtherAddress, 1000);

var transferEvent = tokenService.GetTransferEvent();
var filter = await transferEvent.CreateFilterAsync(new BlockParameter(transferReceipt.BlockNumber));
var logs = await transferEvent.GetAllChangesAsync(filter);

await tokenService.ApproveRequestAndWaitForReceiptAsync(OtherAddress, 500);
var allowance = await tokenService.AllowanceQueryAsync(OwnerAddress, OtherAddress);
```

The deployer holds the whole `InitialAmount`; `logs` holds one `TransferEventDTO` (`From`, `To`, `Value`). `web3` is a `Web3.Web3` whose account owns `OwnerAddress`; in the test it comes from an in-process `DevChainNode` (`node.CreateWeb3(privateKey)`), but any `Web3` works. Backed by `StandardTokenServicesDevChainTests.ShouldDeployATokenTransferApproveAndReadTheTransferEvent` (`DocSection.SmartContracts`, `standard-token-eip20`) in `tests/Nethereum.StandardTokenEIP20.IntegrationTests/StandardTokenServicesDevChainTests.cs`.

## Messages and events

Namespace `Nethereum.StandardTokenEIP20.ContractDefinition`.

```csharp
public partial class EIP20Deployment : EIP20DeploymentBase
{
    public EIP20Deployment()
    public EIP20Deployment(string byteCode)
}

public class EIP20DeploymentBase : ContractDeploymentMessage
{
    public static string BYTECODE
    public virtual BigInteger InitialAmount { get; set; }
    public virtual string TokenName { get; set; }
    public virtual byte DecimalUnits { get; set; }
    public virtual string TokenSymbol { get; set; }
}
```

(`BYTECODE` is empty when the package is built with `BYTECODELITE`.)

Function messages (each a `FunctionMessage`): `NameFunction`, `SymbolFunction`, `DecimalsFunction`, `TotalSupplyFunction`, `BalanceOfFunction` (`Owner`), `BalancesFunction` (`Address`), `AllowanceFunction` (`Owner`, `Spender`), `AllowedFunction` (`Owner`, `Spender`), `TransferFunction` (`To`, `Value`), `TransferFromFunction` (`From`, `To`, `Value`) and `ApproveFunction` (`Spender`, `Value`). Output DTOs: `AllowedOutputDTO`, `BalanceOfOutputDTO`, `SymbolOutputDTO`, `AllowanceOutputDTO` and others in the same file.

Event DTOs:

```csharp
[Event("Transfer")]
public class TransferEventDTOBase : IEventDTO
{
    public virtual string From { get; set; }
    public virtual string To { get; set; }
    public virtual BigInteger Value { get; set; }
}

[Event("Approval")]
public class ApprovalEventDTOBase : IEventDTO
{
    public virtual string Owner { get; set; }
    public virtual string Spender { get; set; }
    public virtual BigInteger Value { get; set; }
}
```

`TransferEventDTO` and `ApprovalEventDTO` are the `partial` classes deriving from these. The older `Nethereum.StandardTokenEIP20.Events.DTO.Transfer` and `Approval` classes are `[Obsolete]`.

## Tests

`tests/Nethereum.StandardTokenEIP20.IntegrationTests/Erc20TokenTester.cs` runs the same operations against a local Geth node and Infura mainnet, so it needs those endpoints; it is not used for the example above.
