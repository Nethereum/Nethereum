---
name: smart-contract-interaction
description: Interact with Ethereum smart contracts using Nethereum typed DTOs. Use this skill whenever the user asks about deploying contracts, calling contract functions, sending transactions to contracts, decoding events, querying historical state, estimating gas, offline signing, or non-type-safe ABI interaction in C#/.NET.
user-invocable: true
---

# Smart Contract Interaction

Nethereum uses typed C# classes (DTOs) to represent every part of a smart contract — deployment bytecode, function calls, events, and return values. This gives you compile-time safety: if a parameter name or type is wrong, you catch it before sending a transaction, not after.

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Contract DTO Definitions

Every interaction starts with defining C# classes that map to the Solidity contract. These can be hand-written or auto-generated with `Nethereum.Generator.Console`.

### Deployment Message

A deployment message represents the contract constructor. It inherits from `ContractDeploymentMessage` and holds the compiled bytecode plus any constructor parameters:

```csharp
public class StandardTokenDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x60606040...";
    public StandardTokenDeployment() : base(BYTECODE) { }

    [Parameter("uint256", "totalSupply")]
    public BigInteger TotalSupply { get; set; }
}
```

### Function Messages

Each contract function maps to a class inheriting from `FunctionMessage`. The `[Function]` attribute takes the Solidity function name and return type:

```csharp
[Function("balanceOf", "uint256")]
public class BalanceOfFunction : FunctionMessage
{
    [Parameter("address", "_owner", 1)]
    public string Owner { get; set; }
}

[Function("transfer", "bool")]
public class TransferFunction : FunctionMessage
{
    [Parameter("address", "_to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 2)]
    public BigInteger TokenAmount { get; set; }
}
```

### Event DTOs

Events implement `IEventDTO`. The fourth argument in `[Parameter]` marks indexed parameters, which enable server-side filtering:

```csharp
[Event("Transfer")]
public class TransferEventDTO : IEventDTO
{
    [Parameter("address", "_from", 1, true)]
    public string From { get; set; }

    [Parameter("address", "_to", 2, true)]
    public string To { get; set; }

    [Parameter("uint256", "_value", 3, false)]
    public BigInteger Value { get; set; }
}
```

### Function Output DTOs

For functions returning complex values, define an output DTO implementing `IFunctionOutputDTO`:

```csharp
[FunctionOutput]
public class BalanceOfOutputDTO : IFunctionOutputDTO
{
    [Parameter("uint256", "balance", 1)]
    public BigInteger Balance { get; set; }
}
```

## Deploy

The deployment handler encodes constructor parameters into the bytecode, estimates gas, and waits for the receipt. The returned `ContractAddress` is where your contract lives:

```csharp
var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
var contractAddress = receipt.ContractAddress;
```

Gas, gas price, and nonce are estimated automatically. Set them on the message only when you need explicit control.

## Query (Read-Only)

Use `GetContractQueryHandler<T>` to call `view` or `pure` functions. These are free — no gas, no transaction:

```csharp
var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

// Single return value
var balance = await balanceHandler.QueryAsync<BigInteger>(contractAddress,
    new BalanceOfFunction { Owner = ownerAddress });

// Deserialize to output DTO (for multiple return values)
var output = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceOfMessage, contractAddress);
```

## Transact (State-Changing)

Use `GetContractTransactionHandler<T>` for functions that modify state. This sends a real transaction and costs gas:

```csharp
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var transfer = new TransferFunction { To = receiverAddress, TokenAmount = 100 };
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);
```

## Decode Events from Receipt

After a transaction, extract events from the receipt logs. This decodes all matching events — if the transaction triggered multiple transfers, you get them all:

```csharp
var transferEvents = receipt.DecodeAllEvents<TransferEventDTO>();
var from = transferEvents[0].Event.From;
var to = transferEvents[0].Event.To;
var value = transferEvents[0].Event.Value;
```

## Historical State Query

All query methods accept `BlockParameter` to read contract state at a past block. The node must have archive state for the requested block:

```csharp
var historicalBalance = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceOfMessage, contractAddress,
    new BlockParameter(deployReceipt.BlockNumber));
```

## Gas Estimation

Nethereum auto-estimates gas. For manual control — useful when you want to display costs or set a hard limit:

```csharp
var estimate = await transferHandler.EstimateGasAsync(contractAddress, transfer);
transfer.Gas = estimate.Value;
transfer.GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei);
```

## Payable Functions

If a Solidity function is `payable`, set `AmountToSend` to include ETH with the call:

```csharp
transfer.AmountToSend = Web3.Convert.ToWei(1); // 1 ETH
```

## Nonce Control

Nethereum manages nonces automatically, including an in-memory counter for rapid submissions. Only set manually for specific ordering needs:

```csharp
transfer.Nonce = 42;
```

## Offline Signing

Sign without broadcasting — useful for cold wallets or deferred submission. You must set `Nonce`, `Gas`, and `GasPrice` since there's no node to query:

```csharp
transfer.Nonce = 2;
transfer.Gas = 60000;
transfer.GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei);
var signedTx = await transferHandler.SignTransactionAsync(contractAddress, transfer);
```

## Non-Type-Safe (ABI JSON)

When you have a raw ABI string and don't need typed classes. This approach is less safe — parameter mismatches fail at runtime, not compile time:

```csharp
var contract = web3.Eth.GetContract(abi, contractAddress);
var balanceFunction = contract.GetFunction("balanceOf");
var transferFunction = contract.GetFunction("transfer");

var balance = await balanceFunction.CallAsync<BigInteger>(ownerAddress);
var gas = await transferFunction.EstimateGasAsync(senderAddress, null, null, toAddress, amount);
var txReceipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
    senderAddress, gas, null, null, toAddress, amount);
```

## When to Use Which Handler

| Handler | When | Gas Cost |
|---------|------|----------|
| `GetContractQueryHandler<T>` | Read-only (`view`/`pure` functions) | Free |
| `GetContractTransactionHandler<T>` | State-changing functions | Yes |
| `GetContractDeploymentHandler<T>` | Deploying new contracts | Yes |

For standard tokens (ERC-20, ERC-721, ERC-1155), use the built-in services (`web3.Eth.ERC20`, `web3.Eth.ERC721`) instead of defining custom DTOs.

For full documentation, see: https://docs.nethereum.com/docs/smart-contracts/guide-smart-contract-interaction
