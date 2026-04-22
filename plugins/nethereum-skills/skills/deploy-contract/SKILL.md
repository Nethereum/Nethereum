---
name: deploy-contract
description: "Deploy smart contracts to Ethereum using Nethereum typed deployment handlers (.NET/C#). Use when the user asks about deploying a contract, publishing a smart contract, contract deployment, constructor parameters, deployment gas estimation, or getting a contract address after deployment."
user-invocable: true
---

# Deploy a Contract

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Typed Deployment (Recommended)

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

```csharp
var account = new Account("0xYOUR_PRIVATE_KEY");
var web3 = new Web3(account, "https://your-rpc-url");

var handler = web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
var receipt = await handler.SendRequestAndWaitForReceiptAsync(
    new StandardTokenDeployment { TotalSupply = Web3.Convert.ToWei(1000000) });

Console.WriteLine($"Deployed at: {receipt.ContractAddress}");
```

## Estimate Gas Before Deploying

```csharp
var deployment = new StandardTokenDeployment { TotalSupply = Web3.Convert.ToWei(1000000) };
var estimatedGas = await handler.EstimateGasAsync(deployment);
Console.WriteLine($"Estimated gas: {estimatedGas.Value}");
```

## Deploy Without Code-Generated Classes

```csharp
var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    abi, bytecode, account.Address,
    new Nethereum.Hex.HexTypes.HexBigInteger(3000000),
    null, null, constructorArg1, constructorArg2);
```

## Multiple Constructor Parameters

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

## Error Handling

```csharp
try
{
    var receipt = await handler.SendRequestAndWaitForReceiptAsync(deployment);
    if (receipt.Status.Value == 0)
        throw new Exception($"Constructor reverted. Gas used: {receipt.GasUsed.Value}");
    Console.WriteLine($"Deployed at: {receipt.ContractAddress}");
}
catch (Nethereum.RPC.Eth.DTOs.ContractDeploymentException ex)
{
    // Bytecode or constructor encoding error
    Console.Error.WriteLine($"Deployment failed: {ex.Message}");
}
catch (RpcResponseException ex)
{
    // RPC-level failure (insufficient funds, nonce too low, gas too low)
    Console.Error.WriteLine($"RPC error: {ex.Message}");
}
```

Common failure causes:
- **`receipt.Status == 0`**: Constructor `require` failed or ran out of gas. Check constructor arguments and increase gas limit if needed.
- **"insufficient funds"**: Account balance too low to cover gas cost. Fund the account or reduce gas price.
- **"nonce too low"**: Pending transaction conflict. Wait for pending transactions to confirm or manually set the nonce.
- **Null `ContractAddress`**: Transaction was sent but not mined as a contract creation. Verify bytecode is not empty.

For full documentation, see: https://docs.nethereum.com/docs/smart-contracts/deploy-a-contract
