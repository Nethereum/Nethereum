---
name: create2-deployment
description: Deploy contracts to deterministic addresses using CREATE2 with Nethereum (.NET). Use this skill whenever the user asks about CREATE2, deterministic deployment, counterfactual addresses, predictable contract addresses, cross-chain same-address deployment, deployment proxies, or address prediction in C#/.NET.
user-invocable: true
---

# CREATE2 Deterministic Deployment

CREATE2 lets you deploy a contract to a predictable address that depends only on the deployer, a salt, and the bytecode. This is useful for counterfactual deployments (predicting addresses before deployment), deploying the same contract to the same address across multiple chains, and factory patterns like Account Abstraction.

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

The service is accessible via `web3.Eth.Create2DeterministicDeploymentProxyService`.

## How the Address is Computed

The CREATE2 address formula is:

```
address = keccak256(0xff ++ deployerAddress ++ salt ++ keccak256(bytecode))[12:]
```

Because the address depends only on these inputs, you can compute it before deployment — and anyone with the same inputs gets the same result.

## Predict Address

Calculate the address without deploying. This is a pure local computation — no transaction, no gas:

```csharp
var create2Service = web3.Eth.Create2DeterministicDeploymentProxyService;
var salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

var predictedAddress = create2Service
    .CalculateCreate2Address<MyContractDeployment>(
        new MyContractDeployment { /* constructor args */ },
        proxyAddress,
        salt);
```

## Check If Already Deployed

Before deploying, verify the contract doesn't already exist at the predicted address. This avoids wasting gas on a transaction that will fail:

```csharp
var alreadyDeployed = await create2Service
    .HasContractAlreadyDeployedAsync(predictedAddress);
```

## Deploy via CREATE2 Proxy

The deployment goes through a deterministic proxy contract. The proxy forwards the bytecode + salt to the CREATE2 opcode:

```csharp
var salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

var result = await create2Service
    .DeployContractRequestAndWaitForReceiptAsync<MyContractDeployment>(
        new MyContractDeployment { /* constructor args */ },
        proxyAddress,
        salt);

Console.WriteLine($"Deployed to: {result.ContractAddress}");
```

The result's `ContractAddress` will match the predicted address exactly.

## Deploy the Proxy

If the deterministic deployment proxy isn't deployed on your chain yet (common on private/app chains), deploy it first:

```csharp
var proxyAddress = await create2Service
    .DeployProxyAndGetContractAddressAsync();
```

On public networks (Mainnet, Sepolia, Polygon, etc.), the standard proxy is usually already deployed.

## Check Proxy Exists

```csharp
var proxyDeployed = await create2Service
    .HasProxyBeenDeployedAsync(proxyAddress);
```

## EIP-155 Chain-Specific Deployment

For deployments that include the chain ID (producing a different proxy address per chain, but deterministic on each):

```csharp
var deployment = await create2Service
    .GenerateEIP155DeterministicDeploymentAsync();
```

## Common Patterns

| Task | Method |
|------|--------|
| Predict address | `CalculateCreate2Address<T>(deployment, proxy, salt)` |
| Check if deployed | `HasContractAlreadyDeployedAsync(address)` |
| Deploy contract | `DeployContractRequestAndWaitForReceiptAsync<T>(deployment, proxy, salt)` |
| Deploy proxy | `DeployProxyAndGetContractAddressAsync()` |
| Check proxy | `HasProxyBeenDeployedAsync(proxyAddress)` |

For full documentation, see: https://docs.nethereum.com/docs/smart-contracts/guide-create2-deployment
