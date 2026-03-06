# Nethereum.AccountAbstraction.SimpleAccount

Reference ERC-4337 SimpleAccount implementation with factory deployment, UserOperation creation, and EntryPoint integration.

## Overview

Nethereum.AccountAbstraction.SimpleAccount provides the canonical SimpleAccount smart contract service - the reference implementation of an ERC-4337 smart account. It includes the `SimpleAccountFactoryService` for deploying new accounts with deterministic addresses and the `SimpleAccountService` for interacting with deployed accounts.

SimpleAccount validates UserOperations by recovering the ECDSA signature and comparing it against the account owner. It supports single and batch execution through the EntryPoint contract. This package is ideal for learning ERC-4337 concepts and as a starting point for custom account implementations.

### Key Features

- **Factory Service**: Deploy SimpleAccount instances with deterministic addresses via CREATE2
- **Account Deployment**: One-step deployment through UserOperation `initCode`
- **Init Code Generation**: `GetCreateAccountInitCode` produces factory calldata for account creation
- **Address Prediction**: `CreateAccountQueryAsync` computes the account address before deployment

## Installation

```bash
dotnet add package Nethereum.AccountAbstraction.SimpleAccount
```

### Dependencies

- **Nethereum.AccountAbstraction** - Core ERC-4337 types (`UserOperation`, `EntryPointService`, gas estimation)
- **Nethereum.Web3** - Web3 instance for contract interaction
- **Nethereum.Contracts** - Contract service base classes

## Quick Start

```csharp
using Nethereum.AccountAbstraction.SimpleAccount;

var factory = new SimpleAccountFactoryService(web3, factoryAddress);

// Predict the account address
var accountAddress = await factory.CreateAccountQueryAsync(
    ownerKey.GetPublicAddress(), salt: 0);

// Get init code for deployment via UserOperation
byte[] initCode = factory.GetCreateAccountInitCode(
    ownerKey.GetPublicAddress(), salt: 0);
```

## Usage Examples

### Example 1: Deploy and Use SimpleAccount

```csharp
using Nethereum.AccountAbstraction.SimpleAccount;
using Nethereum.AccountAbstraction;

var factory = new SimpleAccountFactoryService(web3, factoryAddress);

// Predict address
var accountAddress = await factory.CreateAccountQueryAsync(
    ownerKey.GetPublicAddress(), salt: 0);

// Pre-fund the address
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(accountAddress, 0.1m);

// Create and deploy via UserOperation
var receipt = await factory.CreateAndDeployAccountAsync(
    ownerKey, entryPointService, salt: 0);
```

### Example 2: Generate Init Code for UserOperation

```csharp
// Get init code to include in a UserOperation
byte[] initCode = factory.GetCreateAccountInitCode(
    ownerKey.GetPublicAddress(), salt: 0);

// Use in UserOperation for first-time deployment
var userOp = new UserOperation
{
    Sender = accountAddress,
    InitCode = initCode,
    CallData = executeCallData,
    // ... gas parameters
};
```

## API Reference

### SimpleAccountFactoryService

Factory for deploying SimpleAccount instances.

Key methods:
- `GetCreateAccountInitCode(owner, salt) : byte[]` - Generate factory + calldata for init code
- `CreateAccountQueryAsync(owner, salt) : string` - Predict account address via CREATE2
- `CreateAndDeployAccountAsync(ownerKey, entryPointService, salt)` - Full deployment flow

### SimpleAccountService

Generated contract service for interacting with deployed SimpleAccount.

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 framework

### See Also
- **[Nethereum.AccountAbstraction.SmartContracts](../Nethereum.AccountAbstraction.SmartContracts/README.md)** - Modular smart account with ERC-7579 support

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
