# Nethereum.AccountAbstraction.AppChain

> **PREVIEW** — This package is in preview. APIs may change between releases.

ERC-4337 Account Abstraction for [Nethereum AppChain](../Nethereum.AppChain/README.md) — simplified user onboarding with sponsored gas, admin-controlled whitelisting, and automated infrastructure deployment.

## Overview

AppChain operation is centralised, and this package takes advantage of that trust model to simplify account abstraction. It automates the deployment of all required ERC-4337 infrastructure (EntryPoint, AccountFactory, AccountRegistry, SponsoredPaymaster) and provides a high-level `AppChainAccountAdminService` API for account management, user invitations, and sponsored gas operations.

Unlike the standard bundler which enforces strict ERC-7562 validation and reputation tracking for public mempools, the AppChain variant operates with admin-controlled whitelists and pre-funded paymaster sponsorship — your business, your rules for user onboarding.

### Key Features

- **Automated Deployment**: `AADeployer` deploys all ERC-4337 contracts in one step
- **Account Registry**: Whitelist-based account authorization with invite/ban controls
- **Sponsored Gas**: Built-in `SponsoredPaymaster` for gasless user transactions
- **Admin Controls**: Invite users, ban accounts, manage access
- **Simplified Validation**: No strict ERC-7562 rules (trust-based environment)

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.AppChain
```

### Dependencies

- **Nethereum.AccountAbstraction** - Core ERC-4337 types and services
- **Nethereum.AccountAbstraction.Bundler** - Bundler configuration
- **Nethereum.AppChain** - Core AppChain abstraction (IAppChain, genesis)

Nethereum.Web3 and Nethereum.Contracts are pulled in transitively.

## Quick Start

```csharp
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.Web3;

// Deploy all AA infrastructure
var deployer = new AADeployer(web3);
var deployment = await deployer.DeployAsync(new AppChainConfig
{
    Owner = ownerAddress,
    InitialPaymasterDeposit = 10 // ETH; AADeployer converts to wei internally
});

// Use the high-level service
var aaService = new AppChainAccountAdminService(web3, deployment);

// Invite a user
await aaService.InviteUserAsync(userAddress);

// Predict the account address for the user's config (stable before deployment)
var accountConfig = new AppChainAccountConfig { Owner = userAddress, Salt = 0 };
var accountAddress = await aaService.GetAccountAddressForConfigAsync(accountConfig);
```

## Usage Examples

### Example 1: Full Deployment

```csharp
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.Web3;

var deployer = new AADeployer(web3);
var deployment = await deployer.DeployAsync(new AppChainConfig
{
    Owner = ownerAddress,
    Admins = new[] { admin1, admin2 },
    InitialPaymasterDeposit = 100 // ETH; AADeployer converts to wei internally
});

Console.WriteLine($"EntryPoint: {deployment.EntryPointAddress}");
Console.WriteLine($"Factory: {deployment.AccountFactoryAddress}");
Console.WriteLine($"Registry: {deployment.AccountRegistryAddress}");
Console.WriteLine($"Paymaster: {deployment.SponsoredPaymasterAddress}");
```

### Example 2: User Management

```csharp
var service = new AppChainAccountAdminService(web3, deployment);

// Invite a user (adds to whitelist)
await service.InviteUserAsync(userAddress);

// Check user status
bool invited = await service.IsInvitedAsync(userAddress);
bool active = await service.IsActiveAsync(userAddress);

// Ban a user
await service.BanUserAsync(userAddress, "Policy violation");
```

### Example 3: Provision a modular account

Provisioning composes the owner-validator init data from the deployment's ECDSAValidator module, so an
AppChain account is a standard modular `NethereumSmartAccount` the on-ramp client can drive. The service
deploys the account contract but never signs a user's operations - the owner installs further modules and
operates with their own key.

```csharp
var service = new AppChainAccountAdminService(web3, deployment);
var accountConfig = new AppChainAccountConfig { Owner = ownerAddress, Salt = 0 };

// Predict the account address (stable before deployment)
var accountAddress = await service.GetAccountAddressForConfigAsync(accountConfig);

// Enroll: invite -> provision (deploy) -> activate, in one admin flow (idempotent on retry)
await service.EnrollAccountAsync(accountConfig);

// Or provision without the registry overlay
await service.ProvisionAccountAsync(accountConfig);

bool deployed = await service.IsAccountDeployedAsync(accountAddress);
var nonce = await service.GetNonceAsync(accountAddress, key: 0);
```

## API Reference

### AppChainAccountAdminService

High-level AppChain Account Abstraction API.

```csharp
public class AppChainAccountAdminService
{
    // User management
    public Task<string> InviteUserAsync(string userAddress);
    public Task<string> BanUserAsync(string userAddress, string reason);
    public Task<bool> IsInvitedAsync(string userAddress);
    public Task<bool> IsActiveAsync(string userAddress);

    // Provision modular accounts (owner init data composed from the ECDSAValidator module)
    public Task<string> GetAccountAddressForConfigAsync(AppChainAccountConfig accountConfig);
    public Task<string> ProvisionAccountAsync(AppChainAccountConfig accountConfig);
    public Task<string> EnrollAccountAsync(AppChainAccountConfig accountConfig);

    // Low-level escape hatch (hand-built init data)
    public Task<string> GetAccountAddressAsync(byte[] salt, byte[] initData);
    public Task<string> CreateAccountAsync(byte[] salt, byte[] initData);

    public Task<bool> IsAccountDeployedAsync(string accountAddress);
    public Task<string> ActivateAccountAsync(string accountAddress);
    public Task<BigInteger> GetNonceAsync(string sender, BigInteger key);
}
```

### AADeployer

Automated deployment of all ERC-4337 infrastructure.

```csharp
public class AADeployer
{
    public Task<AppChainDeployment> DeployAsync(AppChainConfig config);
    public Task<bool> IsDeployedAsync(string address);
}
```

### AppChainDeployment

Deployment result containing all contract addresses.

Properties: `EntryPointAddress`, `AccountFactoryAddress`, `AccountRegistryAddress`, `SponsoredPaymasterAddress`, `Modules`

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 framework
- **[Nethereum.AccountAbstraction.Bundler](../Nethereum.AccountAbstraction.Bundler/README.md)** - Bundler configuration
- **[Nethereum.AppChain](../Nethereum.AppChain/README.md)** - Core AppChain abstraction (IAppChain, genesis)

### See Also
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - AppChain server that hosts the bundler

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
