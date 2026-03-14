---
name: deploy-contract
description: Deploy smart contracts to Ethereum using Nethereum typed deployment handlers (.NET/C#). Use this skill whenever the user asks about deploying a contract, publishing a smart contract, contract deployment, constructor parameters, deployment gas estimation, or getting a contract address after deployment.
user-invocable: true
---

# Deploy a Contract

Deploying a contract sends a transaction containing the compiled bytecode and encoded constructor parameters. Nethereum's typed deployment handler takes care of gas estimation, nonce management, and receipt tracking — you just define the deployment message and send it.

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Typed Deployment (Recommended)

Define a deployment message class that inherits from `ContractDeploymentMessage`. It holds the compiled bytecode and maps constructor parameters as properties with `[Parameter]` attributes:

```csharp
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

public class StandardTokenDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x60606040...";
    public StandardTokenDeployment() : base(BYTECODE) { }

    [Parameter("uint256", "totalSupply")]
    public BigInteger TotalSupply { get; set; }
}
```

Then create a `Web3` instance with your account and deploy. The handler encodes the constructor args into the bytecode, estimates gas, and waits for the receipt:

```csharp
var account = new Account("0xYOUR_PRIVATE_KEY");
var web3 = new Web3(account, "https://your-rpc-url");

var handler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var receipt = await handler.SendRequestAndWaitForReceiptAsync(
    new StandardTokenDeployment { TotalSupply = Web3.Convert.ToWei(1000000) });

Console.WriteLine($"Deployed at: {receipt.ContractAddress}");
```

Gas estimation, nonce, and EIP-1559 fees are all automatic.

## Estimate Gas Before Deploying

If you want to check the cost before committing, call `EstimateGasAsync`. This simulates the deployment without sending a transaction:

```csharp
var deployment = new StandardTokenDeployment { TotalSupply = Web3.Convert.ToWei(1000000) };
var estimatedGas = await handler.EstimateGasAsync(deployment);
Console.WriteLine($"Estimated gas: {estimatedGas.Value}");
```

Note: `SendRequestAndWaitForReceiptAsync` estimates gas automatically if you don't set `Gas` on the message — this step is only needed when you want to display costs to a user.

## Deploy Without Code-Generated Classes

For quick prototyping when you have raw ABI and bytecode strings. Less safe — constructor argument types aren't checked at compile time:

```csharp
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi, bytecode, account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(3000000),
    null, null, constructorArg1, constructorArg2);
```

## Multiple Constructor Parameters

Contracts often take multiple constructor arguments. Each maps to a property on the deployment class:

```csharp
public class MyNFTDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = "0x...";
    public MyNFTDeployment() : base(BYTECODE) { }

    [Parameter("string", "name", 1)]
    public string Name { get; set; }

    [Parameter("string", "symbol", 2)]
    public string Symbol { get; set; }

    [Parameter("uint256", "maxSupply", 3)]
    public BigInteger MaxSupply { get; set; }
}

var receipt = await handler.SendRequestAndWaitForReceiptAsync(
    new MyNFTDeployment { Name = "My NFT", Symbol = "MNFT", MaxSupply = 10000 });
```

## Check Deployment Status

The receipt's `Status` tells you if the deployment succeeded. A status of `1` means success; `0` means the constructor reverted (often a `require` failure):

```csharp
if (receipt.Status.Value == 1)
{
    Console.WriteLine($"Deployed at block {receipt.BlockNumber.Value}");
    Console.WriteLine($"Gas used: {receipt.GasUsed.Value}");
    Console.WriteLine($"Contract address: {receipt.ContractAddress}");
}
```

## When to Use Which Approach

| Approach | When |
|----------|------|
| **Typed deployment** (`GetContractDeploymentHandler<T>`) | Recommended for all projects — type-safe, auto gas estimation |
| **Code-generated deployment** | Best — use `Nethereum.Generator.Console` to generate the deployment class from ABI |
| **Raw ABI deployment** (`DeployContract.SendRequestAsync`) | Quick prototyping when you have ABI/bytecode strings |

For full documentation, see: https://docs.nethereum.com/docs/smart-contracts/deploy-a-contract
