---
name: create2-deployment
description: Deploy contracts to deterministic addresses using CREATE2 with Nethereum (.NET). Use this skill whenever the user asks about CREATE2, deterministic deployment, counterfactual addresses, predictable contract addresses, cross-chain same-address deployment, deployment proxies, or address prediction in C#/.NET.
user-invocable: true
---

# CREATE2 Deterministic Deployment

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

CREATE2 deploys contracts to predictable addresses based on deployer, salt, and bytecode. Access via `web3.Eth.Create2DeterministicDeploymentProxyService`.

## Predict Address

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

```csharp
var alreadyDeployed = await create2Service
    .HasContractAlreadyDeployedAsync(predictedAddress);
```

## Deploy via CREATE2 Proxy

```csharp
var salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

var result = await create2Service
    .DeployContractRequestAndWaitForReceiptAsync<MyContractDeployment>(
        new MyContractDeployment { /* constructor args */ },
        proxyAddress,
        salt);

Console.WriteLine($"Deployed to: {result.ContractAddress}");
```

## Deploy the Proxy

If the deterministic proxy isn't deployed on your chain:

```csharp
var proxyAddress = await create2Service
    .DeployProxyAndGetContractAddressAsync();
```

## Check Proxy Exists

```csharp
var proxyDeployed = await create2Service
    .HasProxyBeenDeployedAsync(proxyAddress);
```

## EIP-155 Chain-Specific

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
