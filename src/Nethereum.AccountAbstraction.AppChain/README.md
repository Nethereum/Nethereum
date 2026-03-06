# Nethereum.AccountAbstraction.AppChain

> **PREVIEW** — This package is in preview. APIs may change between releases.

ERC-4337 Account Abstraction for [Nethereum AppChain](../Nethereum.AppChain/README.md) — simplified user onboarding with sponsored gas, admin-controlled whitelisting, and automated infrastructure deployment.

## Overview

AppChain operation is centralised, and this package takes advantage of that trust model to simplify account abstraction. It automates the deployment of all required ERC-4337 infrastructure (EntryPoint, AccountFactory, AccountRegistry, SponsoredPaymaster) and provides a high-level `AppChainService` API for account management, user invitations, and sponsored gas operations.

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
- **Nethereum.Web3** - Web3 instance for contract interaction
- **Nethereum.Contracts** - Contract service base classes

## Quick Start

```csharp
using Nethereum.AccountAbstraction.AppChain;

// Deploy all AA infrastructure
var deployer = new AADeployer(web3);
var deployment = await deployer.DeployAsync(new AppChainConfig
{
    Owner = ownerAddress,
    InitialPaymasterDeposit = Web3.Convert.ToWei(10)
});

// Use the high-level service
var aaService = new AppChainService(web3, deployment);

// Invite a user
await aaService.InviteUserAsync(userAddress);

// Create an account for the user
var accountAddress = await aaService.GetAccountAddressAsync(salt: 0, initData);
```

## Usage Examples

### Example 1: Full Deployment

```csharp
using Nethereum.AccountAbstraction.AppChain;

var deployer = new AADeployer(web3);
var deployment = await deployer.DeployAsync(new AppChainConfig
{
    Owner = ownerAddress,
    Admins = new[] { admin1, admin2 },
    InitialPaymasterDeposit = Web3.Convert.ToWei(100)
});

Console.WriteLine($"EntryPoint: {deployment.EntryPointAddress}");
Console.WriteLine($"Factory: {deployment.AccountFactoryAddress}");
Console.WriteLine($"Registry: {deployment.AccountRegistryAddress}");
Console.WriteLine($"Paymaster: {deployment.SponsoredPaymasterAddress}");
```

### Example 2: User Management

```csharp
var service = new AppChainService(web3, deployment);

// Invite a user (adds to whitelist)
await service.InviteUserAsync(userAddress);

// Check user status
bool invited = await service.IsInvitedAsync(userAddress);
bool active = await service.IsActiveAsync(userAddress);

// Ban a user
await service.BanUserAsync(userAddress, "Policy violation");
```

### Example 3: Account Operations

```csharp
var service = new AppChainService(web3, deployment);

// Predict account address
var accountAddress = await service.GetAccountAddressAsync(salt: 0, initData);

// Check if deployed
bool deployed = await service.IsAccountDeployedAsync(accountAddress);

// Create the account
await service.CreateAccountAsync(salt: 0, initData);

// Get nonce for UserOperation
var nonce = await service.GetNonceAsync(accountAddress, key: 0);
```

## API Reference

### AppChainService

High-level AppChain Account Abstraction API.

```csharp
public class AppChainService
{
    // User management
    public Task InviteUserAsync(string userAddress);
    public Task BanUserAsync(string userAddress, string reason);
    public Task<bool> IsInvitedAsync(string userAddress);
    public Task<bool> IsActiveAsync(string userAddress);

    // Account operations
    public Task<string> GetAccountAddressAsync(BigInteger salt, byte[] initData);
    public Task CreateAccountAsync(BigInteger salt, byte[] initData);
    public Task<bool> IsAccountDeployedAsync(string accountAddress);
    public Task ActivateAccountAsync();
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

### See Also
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - AppChain server that hosts the bundler

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
