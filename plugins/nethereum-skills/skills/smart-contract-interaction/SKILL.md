---
name: smart-contract-interaction
description: Interact with Ethereum smart contracts using Nethereum typed DTOs. Use this skill whenever the user asks about deploying contracts, calling contract functions, sending transactions to contracts, decoding events, querying historical state, estimating gas, offline signing, or non-type-safe ABI interaction in C#/.NET.
user-invocable: true
---

# Smart Contract Interaction

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Contract DTO Definitions

### Deployment Message
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
```csharp
[FunctionOutput]
public class BalanceOfOutputDTO : IFunctionOutputDTO
{
    [Parameter("uint256", "balance", 1)]
    public BigInteger Balance { get; set; }
}
```

## Deploy
```csharp
var deploymentMessage = new StandardTokenDeployment { TotalSupply = 100000 };
var deploymentHandler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
var contractAddress = receipt.ContractAddress;
```

## Query
```csharp
var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

// Single value
var balance = await balanceHandler.QueryAsync<BigInteger>(contractAddress,
    new BalanceOfFunction { Owner = ownerAddress });

// Deserialize to output DTO
var output = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceOfMessage, contractAddress);
```

## Transact
```csharp
var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
var transfer = new TransferFunction { To = receiverAddress, TokenAmount = 100 };
var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);
```

## Decode Events from Receipt
```csharp
var transferEvents = receipt.DecodeAllEvents<TransferEventDTO>();
var from = transferEvents[0].Event.From;
var to = transferEvents[0].Event.To;
var value = transferEvents[0].Event.Value;
```

## Historical State Query
```csharp
var historicalBalance = await balanceHandler.QueryDeserializingToObjectAsync<BalanceOfOutputDTO>(
    balanceOfMessage, contractAddress,
    new BlockParameter(deployReceipt.BlockNumber));
```

## Gas Estimation
```csharp
var estimate = await transferHandler.EstimateGasAsync(contractAddress, transfer);
transfer.Gas = estimate.Value;
transfer.GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei);
```

## Send Ether with Function Call
```csharp
transfer.AmountToSend = Web3.Convert.ToWei(1); // payable functions
```

## Nonce Control
```csharp
transfer.Nonce = 42; // manual nonce
```

## Offline Signing
```csharp
transfer.Nonce = 2;
transfer.Gas = 60000;
transfer.GasPrice = Web3.Convert.ToWei(25, UnitConversion.EthUnit.Gwei);
var signedTx = await transferHandler.SignTransactionAsync(contractAddress, transfer);
```

## Non-Type-Safe (ABI JSON)
```csharp
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi, bytecode, senderAddress, new HexBigInteger(900000), null, totalSupply);

var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);
var balanceFunction = contract.GetFunction("balanceOf");
var transferFunction = contract.GetFunction("transfer");

var balance = await balanceFunction.CallAsync<BigInteger>(ownerAddress);
var gas = await transferFunction.EstimateGasAsync(senderAddress, null, null, toAddress, amount);
var txReceipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
    senderAddress, gas, null, null, toAddress, amount);
```

## Key Rules
- **ALWAYS define typed DTOs** (ContractDeploymentMessage, FunctionMessage, IEventDTO) — catches errors at compile time
- Use `GetContractDeploymentHandler<T>` for deploy, `GetContractQueryHandler<T>` for reads, `GetContractTransactionHandler<T>` for writes
- Gas, gas price, and nonce are auto-managed — only set manually for offline signing or specific control
- For standard tokens use built-in services (`web3.Eth.ERC20`, `web3.Eth.ERC721`) instead of custom DTOs
- Use `receipt.DecodeAllEvents<T>()` to decode events from transaction receipts
- Pass `BlockParameter` to any query method for historical state reads
