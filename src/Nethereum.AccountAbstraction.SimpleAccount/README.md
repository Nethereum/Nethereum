# Nethereum.AccountAbstraction.SimpleAccount

Reference ERC-4337 SimpleAccount implementation with factory deployment, UserOperation creation, and EntryPoint integration.

## Overview

Nethereum.AccountAbstraction.SimpleAccount provides the canonical SimpleAccount smart contract service - the reference implementation of an ERC-4337 smart account. It includes the `SimpleAccountFactoryService` for deploying new accounts with deterministic addresses and the `SimpleAccountService` for interacting with deployed accounts.

SimpleAccount validates UserOperations by recovering the ECDSA signature and comparing it against the account owner. It supports single and batch execution through the EntryPoint contract. This package is ideal for learning ERC-4337 concepts and as a starting point for custom account implementations.

> **Where this fits.** SimpleAccount is the plain ERC-4337 reference/compliance account: no ERC-7579
> validator, no modules - it exists to learn the basics of Account Abstraction and as the account
> deployed by the bundler compliance test estate (`Nethereum.AccountAbstraction.ComplianceHarness`).
> For real applications, the standard/primary path is the modular ERC-7579 `NethereumSmartAccount` in
> **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)**, created and driven
> through `IAAClient` - it adds validators, executors and session modules that SimpleAccount has no way
> to support. `IAAClient` can still attach to an already-deployed SimpleAccount too, via the non-modular
> `GetAccount(address, signingService)` overload; the examples below instead deploy and drive the
> account directly through `SimpleAccountFactoryService`, without `IAAClient`.

### Key Features

- **Factory Service**: Deploy SimpleAccount instances with deterministic addresses via CREATE2
- **Account Deployment**: One-step deployment through UserOperation `initCode`
- **Init Code Generation**: `GetCreateAccountInitCode` produces the factory address concatenated with `createAccount` calldata
- **Address Prediction**: `GetAddressQueryAsync` (and `CreateAccountQueryAsync`, called as a query) compute the account address before deployment

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
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;

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
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction;

var factory = new SimpleAccountFactoryService(web3, factoryAddress);

// Predict address
var accountAddress = await factory.CreateAccountQueryAsync(
    ownerKey.GetPublicAddress(), salt: 0);

// Pre-fund the address
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(accountAddress, 0.1m);

// Create and deploy via UserOperation. This returns a CreateAndDeployAccountResult
// (AccountAddress + Receipt), not a bare TransactionReceipt - and it funds the
// counterfactual address itself, so the pre-fund above is only needed if you want more.
var deployed = await factory.CreateAndDeployAccountAsync(
    ownerKey.GetPublicAddress(), beneficiaryAddress, entryPointAddress,
    ownerKey, fundingAmountInEther: 0.01m, salt: 0);

Console.WriteLine($"{deployed.AccountAddress} deployed in {deployed.Receipt.TransactionHash}");
```

`CreateAndDeployAccountAsync` throws if the account already has code — it is a first-deployment helper, not an idempotent one.

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

Factory for deploying SimpleAccount instances (namespace `Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory`).

Hand-written helpers:

- `byte[] GetCreateAccountInitCode(BigInteger salt)` — uses the Web3 account's address as owner
- `byte[] GetCreateAccountInitCode(string owner, BigInteger salt)` — the factory address concatenated with the `createAccount(owner, salt)` calldata, i.e. a UserOperation's `initCode`
- `Task<string> CreateAccountQueryAsync(string owner, BigInteger salt)` — calls `createAccount` as a query, so it returns the address without deploying
- `Task<CreateAndDeployAccountResult> CreateAndDeployAccountAsync(string owner, string beneficiary, string entryPointAddress, EthECKey ethKey, decimal fundingAmountInEther = 0.01m, ulong salt = DEFAULT_ACCOUNT_CREATION_SALT, ulong callGasLimit = DEFAULT_ACCOUNT_CREATION_CALL_GAS_LIMIT, ulong verificationGasLimit = DEFAULT_ACCOUNT_CREATION_VERIFICATION_GAS_LIMIT, ulong gas = DEFAULT_ACCOUNT_CREATION_GAS)` — the full flow: derive the address, fund it, build and sign an `initCode`-only UserOperation, and submit it through `EntryPoint.handleOps`. A `BigInteger` overload takes the same values without defaults.

Its defaults are public constants: `DEFAULT_ACCOUNT_CREATION_SALT` = 0, `DEFAULT_ACCOUNT_CREATION_CALL_GAS_LIMIT` = 1,000,000, `DEFAULT_ACCOUNT_CREATION_VERIFICATION_GAS_LIMIT` = 2,000,000, `DEFAULT_ACCOUNT_CREATION_GAS` = 10,000,000.

`CreateAndDeployAccountResult` carries `string AccountAddress` and `TransactionReceipt Receipt`.

Generated members (from the contract ABI):

- `Task<string> GetAddressQueryAsync(string owner, BigInteger salt, BlockParameter blockParameter = null)` — the CREATE2 counterfactual address
- `Task<string> AccountImplementationQueryAsync(BlockParameter blockParameter = null)` and `Task<string> SenderCreatorQueryAsync(BlockParameter blockParameter = null)`
- `CreateAccountRequestAsync` / `CreateAccountRequestAndWaitForReceiptAsync(string owner, BigInteger salt, …)` — deploy directly with an ordinary transaction instead of a UserOperation
- `DeployContractAsync` / `DeployContractAndWaitForReceiptAsync` / `DeployContractAndGetServiceAsync(IWeb3 web3, SimpleAccountFactoryDeployment deployment, …)` — the deployment message takes the `EntryPoint` address

### SimpleAccountService

Generated contract service for interacting with a deployed SimpleAccount.

## Related Packages

### Dependencies
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - Core ERC-4337 framework

### See Also
- **[Nethereum.AccountAbstraction](../Nethereum.AccountAbstraction/README.md)** - ERC-7579 modular account support (validators, executors, sessions)

## Additional Resources

- [ERC-4337: Account Abstraction](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com)
