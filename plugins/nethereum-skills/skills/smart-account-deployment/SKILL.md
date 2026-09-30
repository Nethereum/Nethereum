---
name: smart-account-deployment
description: "Help users deploy ERC-4337 smart accounts using Nethereum — create a modular NethereumAccount via IAAClient, predict counterfactual addresses with CREATE2, deploy lazily via InitCode on the first UserOperation, manage EntryPoint deposits. Use when the user mentions deploying a smart account, creating a smart wallet, CREATE2 account address, counterfactual address, NethereumAccountFactory, SimpleAccountFactory, or account factory deployment in .NET/C#."
user-invocable: true
---

# Smart Account Deployment

Deploy ERC-4337 smart accounts with Nethereum — predict addresses before deployment with CREATE2, deploy upfront or lazily via the first UserOperation's `InitCode`.

## When to Use This

- User wants to **deploy a new smart contract wallet**
- User needs to **predict an account address** before deployment (CREATE2)
- User wants **lazy deployment** — deploy the account as part of the first UserOperation
- User is working with `NethereumAccountFactory`, `SimpleAccountFactory`, or a custom account factory

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
dotnet add package Nethereum.AccountAbstraction.SimpleAccount  # only for the SimpleAccount reference account
```

## Recommended: `IAAClient.CreateAccountAsync` (modular NethereumAccount)

The instant on-ramp predicts the counterfactual address and returns a ready-to-use `NethereumSmartAccount` — no deploy transaction needed, the account deploys itself on its first UserOperation:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.Signer;

services.AddNethereumAccountAbstraction(o => o
    .UseWeb3(web3)
    .UseDeploymentAddresses(new AADeploymentAddresses(
        EntryPointAddress: EntryPointAddresses.Latest,
        NethereumAccountFactoryAddress: factoryAddress,
        EcdsaValidatorAddress: ecdsaValidatorAddress,
        VerifyingPaymasterAddress: ""))
    .UseBundlerUrl(bundlerUrl));

var aaClient = serviceProvider.GetRequiredService<IAAClient>();

var owner = EthECKey.GenerateKey();
var account = await aaClient.CreateAccountAsync(owner, salt: null);  // random salt if omitted

Console.WriteLine($"Counterfactual address: {account.Address}");
Console.WriteLine($"Is deployed: {account.IsDeployed}");

// Fund it, then drive any generated typed service through it — the first call deploys it
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(account.Address, 0.1m);

myTokenService.UseAccountAbstraction(account, aaClient);
var receipt = await myTokenService.TransferRequestAndWaitForReceiptAsync(recipient, amount);
```

## Attach to an Already-Deployed Account

```csharp
// Modular NethereumAccount, with its ERC-7579 validator
var account = aaClient.GetAccount(accountAddress, signingService, validator);

// Plain non-modular account (e.g. SimpleAccount) — no validator
var account = aaClient.GetAccount(accountAddress, signingService);

// Shortcut for an already-deployed NethereumAccount's generated service:
var accountService = new NethereumAccountService(web3, accountAddress);
```

## Lower-Level: `NethereumAccountFactoryService` Directly

`IAAClient.CreateAccountAsync` is built on this factory — reach for it directly when you need the raw counterfactual address/init-code without going through the client:

```csharp
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.ERC7579.Modules;

var factory = new NethereumAccountFactoryService(web3, factoryAddress);
var initData = AccountInitDataBuilder.BuildEcdsa(ecdsaValidatorAddress, ownerAddress);
var salt = new byte[32]; // fill with your own CREATE2 salt

// Predict address without deploying
var address = await factory.GetAddressQueryAsync(salt, initData);
var isDeployed = await factory.IsDeployedQueryAsync(salt, initData);
var initCode = await factory.GetInitCodeQueryAsync(salt, initData);
```

## Lazy Deployment via InitCode

Nothing extra to do: `IAAClient.Configure`/`ChangeContractHandlerToAA` check on-chain whether the account exists and attach the factory's `InitCode` to the first UserOperation automatically. On subsequent calls it's omitted. See `Nethereum.AccountAbstraction`'s README for the low-level `FactoryConfig` walkthrough (SimpleAccount-family accounts).

## SimpleAccount (non-modular reference account)

```csharp
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;

var factory = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
    web3, new SimpleAccountFactoryDeployment { EntryPoint = entryPointAddress });

var accountAddress = await factory.GetAddressQueryAsync(ownerKey.GetPublicAddress(), salt: 0);
```

## EntryPoint Deposits (NethereumAccount)

```csharp
var deposit = await accountService.GetDepositQueryAsync();
await accountService.AddDepositAsync(Web3.Convert.ToWei(0.1m));
await accountService.WithdrawDepositToRequestAndWaitForReceiptAsync(withdrawAddress, amount);
```

## EntryPoint Versions

Use `EntryPointAddresses.Latest` (V09) for new deployments. The account is bound to the EntryPoint version at deploy time.

## Common Mistakes

- **Not funding** the counterfactual address before the first UserOperation
- **Changing the salt** between prediction and deployment (produces a different address)
- **Wrong factory address** between prediction and deployment
- **Wrong EntryPoint** — account is bound to one version
- **Mismatched validator** — `GetAccount(address, signingService, validator)` must use the same validator the account was created/installed with, or signature validation fails

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-smart-account-deployment
