---
name: modular-accounts
description: "Help users work with ERC-7579 modular smart accounts using Nethereum — install and manage validators (ECDSA, multisig, social recovery), executors, hooks, and session keys. Use when the user mentions ERC-7579, modular accounts, smart account modules, validators, executors, session keys, multisig wallet, social recovery, OwnableValidator, ECDSAValidator, SmartSession, or modular smart contract wallets in .NET/C#."
user-invocable: true
---

# ERC-7579 Modular Accounts

ERC-7579 defines a standard interface for modular smart accounts. Compose behavior from interchangeable modules — validators control who can sign, executors define what actions are allowed, hooks intercept calls.

## When to Use This

- User wants **multisig** on a smart account (OwnableValidator)
- User needs **social recovery** with guardians
- User wants **session keys** with scoped permissions (SmartSession)
- User is installing/removing **ERC-7579 modules**
- User mentions validators, executors, hooks, or modular accounts

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
```

```csharp
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
```

## Module Types

| Type | Constant | Value | Purpose |
|------|----------|-------|---------|
| Validator | `ERC7579ModuleTypes.TYPE_VALIDATOR` | 1 | Controls who can authorize operations |
| Executor | `ERC7579ModuleTypes.TYPE_EXECUTOR` | 2 | Defines what actions the account can perform |
| Fallback | `ERC7579ModuleTypes.TYPE_FALLBACK` | 3 | Handles unrecognized function calls |
| Hook | `ERC7579ModuleTypes.TYPE_HOOK` | 4 | Intercepts calls before/after execution |

## Install Validators

### ECDSA Validator (Single Signer)

```csharp
var config = ECDSAValidatorConfig.Create(validatorAddress, ownerAddress);
var receipt = await accountService.InstallModuleAndWaitForReceiptAsync(config);

// Or shortcut:
await accountService.InstallECDSAValidatorAndWaitForReceiptAsync(validatorAddress, ownerAddress);
```

### Ownable Validator (Multisig)

```csharp
var config = OwnableValidatorConfig.Create(validatorAddress, threshold: 2, owner1, owner2, owner3);
var receipt = await accountService.InstallModuleAndWaitForReceiptAsync(config);

// Fluent builder:
var config = new OwnableValidatorConfig(validatorAddress)
    .WithOwner(owner1).WithOwner(owner2).WithOwner(owner3)
    .WithThreshold(2);

// Shortcut:
await accountService.InstallOwnableValidatorAndWaitForReceiptAsync(
    validatorAddress, threshold: 2, owner1, owner2, owner3);
```

### Social Recovery

```csharp
var config = SocialRecoveryConfig.Create(moduleAddress, threshold: 2, guardian1, guardian2, guardian3);
var receipt = await accountService.InstallModuleAndWaitForReceiptAsync(config);

// Fluent:
var config = new SocialRecoveryConfig(moduleAddress)
    .WithGuardian(guardian1).WithGuardian(guardian2).WithGuardian(guardian3)
    .WithThreshold(2);

// Shortcut:
await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
    moduleAddress, threshold: 2, guardian1, guardian2, guardian3);
```

## Install Executor

```csharp
var config = OwnableExecutorConfig.Create(executorAddress, delegateAddress);
var receipt = await accountService.InstallModuleAndWaitForReceiptAsync(config);

// Shortcut:
await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(executorAddress, delegateAddress);
```

## Smart Sessions (Session Keys)

Grant temporary, scoped permissions to a key:

```csharp
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;

// Basic session
var config = SmartSessionConfig.Create(sessionModuleAddress, sessionValidatorAddress, salt);

// With owner for ECDSA verification
var config = SmartSessionConfig.CreateWithOwner(
    sessionModuleAddress, sessionValidatorAddress, ownerAddress, salt);

// Scoped permissions
var config = SmartSessionConfig.Create(sessionModuleAddress, sessionValidatorAddress, salt)
    .WithERC20TransferAction(tokenAddress, spendingLimitPolicy, policyInitData)
    .WithUserOpPolicy(policyAddress)
    .WithPaymasterPermission(permit: true);

// Custom action targeting any contract
var config = SmartSessionConfig.Create(sessionModuleAddress, sessionValidatorAddress, salt)
    .WithAction(targetContract, functionSelector);

var receipt = await accountService.InstallModuleAndWaitForReceiptAsync(config);
```

## Check and Remove Modules

```csharp
// Check if installed
bool installed = await accountService.IsModuleInstalledAsync(config);

// Remove
var receipt = await accountService.UninstallModuleAndWaitForReceiptAsync(config);
```

## Configure at Account Creation

Bake modules in from the start with SmartAccountBuilder:

```csharp
var builder = new SmartAccountBuilder()
    .WithValidator(validatorAddress);

// Or with full init data:
var initData = new OwnableValidatorConfig(validatorAddress, threshold: 2, owner1, owner2).GetInitData();
var builder = new SmartAccountBuilder()
    .WithModule(ERC7579ModuleTypes.TYPE_VALIDATOR, validatorAddress, initData);
```

## Common Mistakes

- **Wrong module type** — executor installed as validator won't work
- **Threshold > owner count** — account becomes unusable
- **Removing last validator** — no way to authorize operations
- **Forgetting `.WithPaymasterPermission(true)`** in sessions using paymasters

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-modular-accounts
