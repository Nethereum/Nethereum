---
name: smart-account-deployment
description: "Help users deploy ERC-4337 smart accounts using Nethereum — predict counterfactual addresses with CREATE2, deploy via SmartAccountBuilder, lazy deploy via InitCode, manage EntryPoint deposits. Use when the user mentions deploying a smart account, creating a smart wallet, CREATE2 account address, counterfactual address, SmartAccountBuilder, SimpleAccountFactory, or account factory deployment in .NET/C#."
user-invocable: true
---

# Smart Account Deployment

Deploy ERC-4337 smart accounts using `SmartAccountBuilder` — predict addresses before deployment with CREATE2, deploy upfront or lazily via the first UserOperation's `InitCode`.

## When to Use This

- User wants to **deploy a new smart contract wallet**
- User needs to **predict an account address** before deployment (CREATE2)
- User wants **lazy deployment** — deploy the account as part of the first UserOperation
- User is working with SimpleAccountFactory or custom account factories

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
dotnet add package Nethereum.AccountAbstraction.SimpleAccount
```

## SmartAccountBuilder (Recommended)

The fluent builder predicts addresses, deploys via factory, and returns a ready-to-use service:

```csharp
using Nethereum.AccountAbstraction.Extensions;
using Nethereum.Web3;

var web3 = new Web3(new Nethereum.Web3.Accounts.Account(privateKey), rpcUrl);

// Predict address without deploying
var address = await web3.CreateSmartAccount()
    .WithFactory(factoryAddress)
    .WithOwnerKey(privateKey)
    .WithSalt(0)
    .GetAddressAsync();

// Deploy and get ready-to-use service
var account = await web3.CreateSmartAccount()
    .WithFactory(factoryAddress)
    .WithOwnerKey(privateKey)
    .BuildAsync();

Console.WriteLine($"Deployed at: {account.Address}");
Console.WriteLine($"Is deployed: {await account.IsDeployedAsync()}");
```

### Builder Methods

| Method | Description |
|--------|-------------|
| `WithFactory(string)` | Required. Factory contract address |
| `WithOwner(string)` | Owner address |
| `WithOwnerKey(string)` or `WithOwnerKey(EthECKey)` | Owner from private key |
| `WithSalt(BigInteger)` | CREATE2 salt (default 0) |
| `WithValidator(string)` | ERC-7579 validator module address |
| `WithModule(BigInteger, string, byte[])` | Add module (type, address, init data) |
| `FromExisting(string)` | Load already-deployed account |
| `BuildAsync()` | Deploy (if needed) → `SmartAccountService` |
| `GetAddressAsync()` | Counterfactual address without deploying |
| `GetInitCodeAsync()` | Init code bytes for UserOperation |

## Load Existing Account

```csharp
// Via builder
var account = await web3.CreateSmartAccount()
    .FromExisting(accountAddress)
    .BuildAsync();

// Shortcut
var account = await web3.GetSmartAccountAsync(accountAddress);
```

## Lazy Deployment via InitCode

Deploy the account as part of the first UserOperation:

```csharp
// Get init code from builder
var builder = web3.CreateSmartAccount()
    .WithFactory(factoryAddress)
    .WithOwnerKey(privateKey);

var initCode = await builder.GetInitCodeAsync();
var address = await builder.GetAddressAsync();

// Set on UserOperation
var userOp = new UserOperation
{
    Sender = address,
    InitCode = initCode,
    // ... other fields
};
```

### Automatic with AAContractHandler

```csharp
erc20Service.ChangeContractHandlerToAA(
    counterfactualAddress, privateKey, bundlerUrl, entryPointAddress,
    new FactoryConfig
    {
        FactoryAddress = factoryAddress,
        Owner = ownerAddress,
        Salt = 0
    });

// First call deploys + executes atomically
var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
    new TransferFunction { To = recipient, Value = amount });
```

## EntryPoint Deposits

```csharp
var deposit = await account.GetDepositAsync();
await account.AddDepositAsync(Web3.Convert.ToWei(0.1m));
await account.WithdrawDepositToAsync(withdrawAddress, amount);
```

## EntryPoint Versions

Use `EntryPointAddresses.Latest` (V09) for new deployments. The account is bound to the EntryPoint version at deploy time.

## Common Mistakes

- **Not funding** the counterfactual address before the first UserOperation
- **Changing the salt** between prediction and deployment (produces different address)
- **Wrong factory address** between prediction and deployment
- **Wrong EntryPoint** — account is bound to one version

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-smart-account-deployment
