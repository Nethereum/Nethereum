# Nethereum Account Abstraction (ERC-4337)

Let a smart contract be the account. Instead of an EOA signing a transaction, your user owns a contract wallet that runs a **UserOperation** — so gas can be sponsored by someone else, several calls can land atomically, the key can live in a passkey or an HSM, and the account can deploy itself on its very first use. This package is the client side of that: you keep writing ordinary Nethereum contract services, and one call reroutes them through Account Abstraction.

## What you can do with it

| Task | Reach for |
|---|---|
| Send a call from a smart account instead of an EOA | `service.UseAccountAbstraction(account, client)`, then any `…RequestAndWaitForReceiptAsync()` |
| Create an account that deploys itself on its first operation | `IAAClient.CreateAccountAsync(owner)` |
| Own that account with a hardware wallet, KMS/HSM, MPC or a passkey | `IAAClient.CreateAccountAsync(signingService, validator, initData)` |
| Turn an existing funded EOA into a smart account **at the same address** (EIP-7702) | `Eip7702AAClientExtensions.CreateEip7702Account` + `IAAClient.ConfigureEip7702` |
| Run several contract calls atomically inside one operation | `AAContractHandler.BatchExecuteAsync(...)` |
| Have somebody else pay the gas | `AAContractHandler.WithPaymaster(...)`, and `IPaymasterManager` to run the paymaster |
| Find out whether the operation really succeeded on-chain — and why not | `AATransactionReceipt.UserOpSuccess` / `.RevertReason` / `.FailureDiagnostic` |
| Attach ERC-7579 modules: session keys, spending limits, social recovery | `SmartSessionConfig`, `OwnableValidatorConfig`, the module services |
| Enforce composable on-chain authorisation rules (unstaked, under the conditions below) | the [rule engine](#authorization-rules-on-chain-rule-engine): `RuleRegistry` + a combinator |

You also need somewhere to send the operation. Point the client at a public bundler URL, or run one in-process with **Nethereum.AccountAbstraction.Bundler**.

## Install

```bash
dotnet add package Nethereum.AccountAbstraction
```

That package carries the client, the account model, the handler, the ERC-7579 modules and the rule engine. Two examples below reach outside it, so add the package they need:

| Add | When you need |
|---|---|
| `Nethereum.AccountAbstraction.SimpleAccount` | `SimpleAccountFactoryService` / `SimpleAccountFactoryDeployment` — the `FactoryConfig` walkthrough in the Advanced section |
| `Nethereum.StandardTokenEIP20` | `EIP20Deployment` — only if you want to *deploy* a token to try the ERC-20 examples against; reading and transferring an existing one through `web3.Eth.ERC20` does not need it |

`Nethereum.AccountAbstraction` itself references only `Nethereum.ABI`, `Nethereum.Web3` and `Microsoft.Extensions.DependencyInjection.Abstractions`, so neither arrives transitively. `web3.Eth.ERC20` needs nothing extra — it comes from `Nethereum.Contracts`, via `Nethereum.Web3`.

## Quick Start — send a UserOperation and read its receipt

Extracted from `OnRampTests.Given_AddNethereumAccountAbstraction_When_CreateAccount_and_send_typed_op_Then_lands_in_a_few_lines` (`tests/Nethereum.AccountAbstraction.IntegrationTests/E2E/ModularAccount/OnRampTests.cs`), which runs this against a real EntryPoint and a real bundler.

```csharp
var services = new ServiceCollection();
services.AddNethereumAccountAbstraction(o => o
    .UseWeb3(web3)
    .UseDeploymentAddresses(new AADeploymentAddresses(
        entryPointAddress,
        accountFactoryAddress,
        ecdsaValidatorAddress,
        VerifyingPaymasterAddress: string.Empty))
    .UseBundlerUrl("https://your-bundler-url"));

var client = services.BuildServiceProvider().GetRequiredService<IAAClient>();

var owner = EthECKey.GenerateKey();
var account = await client.CreateAccountAsync(owner);      // account.IsDeployed == false

// The counterfactual address pays for its own deployment and its first operation, so fund it first.
// (The test seeds the balance directly on its devchain; on a real chain, transfer to it.)
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(account.Address, 1m);

// counterService is a code-generated contract service - those extend ContractWeb3ServiceBase,
// which is what UseAccountAbstraction/Configure require. The test uses its TestCounterService.
counterService.UseAccountAbstraction(account, client);
var receipt = (AATransactionReceipt)await counterService.CountRequestAndWaitForReceiptAsync();

Console.WriteLine($"{receipt.UserOpHash} success={receipt.UserOpSuccess} {receipt.RevertReason}");
```

That one call did four things at once: it built the UserOperation, deployed the account through its factory (its `InitCode`), executed `count()` from the account's own address, and came back with an `AATransactionReceipt` whose `Sender` is the account. After it, `eth_getCode` on `account.Address` is no longer empty. Skip the funding step and the operation never reaches the chain — the bundler rejects it at estimation with ERC-4337 error `AA21`, which the sibling test `Given_unfunded_account_created_via_client_When_send_typed_op_Then_AA21_minus32500` pins.

`UseBundlerUrl(...)` talks to an external bundler over JSON-RPC; `UseBundler(myBundlerService)` takes an in-process `IAccountAbstractionBundlerService` instead. `UseWeb3` can be omitted when an `IWeb3` is already registered in the container.

## Entry points

**Reach for `IAAClient` first.** It is the whole client surface: it makes accounts (`CreateAccountAsync`), attaches to existing ones (`GetAccount`), and reroutes any generated contract service through Account Abstraction (`Configure` / `ConfigureEip7702`). Register it once with `AddNethereumAccountAbstraction` and everything else in this README hangs off the account and the handler it hands back.

| I want to... | Use |
|---|---|
| register the client (Web3 + deployed addresses + bundler) | `services.AddNethereumAccountAbstraction(o => …)` → `IAAClient` |
| make a new modular account for an `EthECKey` owner | `IAAClient.CreateAccountAsync(owner, salt)` |
| make one for any signer (hardware / KMS / MPC / passkey) | `IAAClient.CreateAccountAsync(signingService, validator, initData, salt)` |
| attach to an account that already exists on-chain | `IAAClient.GetAccount(address, signingService, validator)` |
| send through a **code-generated** contract service | `service.UseAccountAbstraction(account, client)` — shorthand for `IAAClient.Configure(service, account)`; both constrained `where TService : ContractWeb3ServiceBase` |
| send through a **built-in standard** service (`ERC20ContractService`, `ERC721ContractService`, ENS, …) | `service.SwitchToAccountAbstraction(accountAddress, signerKey, bundlerService, entryPointAddress)` — these implement `IContractHandlerService` only, so `Configure`/`UseAccountAbstraction` do not bind to them |
| reach the fluent knobs (paymaster, gas, batching) | the `AAContractHandler` that `Configure` returns |
| batch calls atomically | `AAContractHandler.BatchExecuteAsync(...)` |
| sponsor gas | `AAContractHandler.WithPaymaster(address \| PaymasterConfig)` |
| read the outcome | `AATransactionReceipt` — `UserOpSuccess`, `RevertReason`, `FailureDiagnostic`, `ActualGasCost` |
| pick an EntryPoint address | `EntryPointAddresses.Latest` (v0.9); v0.7/v0.6 are not supported for signing |

## What is Account Abstraction?

Account Abstraction (ERC-4337) allows smart contracts to act as user accounts. Instead of sending transactions directly, you create **UserOperations** that are:

1. Signed by your key (or any other `IAccountSigningService` - hardware/KMS/MPC, WebAuthn, ...)
2. Sent to a **Bundler** (not directly to the blockchain)
3. Executed by the **EntryPoint** contract via `handleOps`

Benefits include:
- **Gas sponsorship** - Paymasters can pay gas on behalf of users
- **Batched transactions** - Multiple calls in a single operation
- **Custom validation** - Social recovery, multi-sig, session keys (ERC-7579 modules)
- **Account deployment** - Create accounts on first use

## Setting up: the client, the account model, and the account tiers

### 1. Register the client (`AddNethereumAccountAbstraction`)

One DI call wires up an `IAAClient`: give it your `Web3`, the network's deployed AA contract addresses, and a bundler.

The EntryPoint is the contract that runs `handleOps`. Nethereum knows the canonical deployments:

```csharp
public static class EntryPointAddresses
{
    public const string V09 = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
    public const string V08 = "0x4337084d9e255ff0702461cf8895ce9e3b5ff108";
    public const string V07 = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
    public const string V06 = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";

    public static string Latest { get; }     // == V09

    public static void ValidateSupportedUserOpHashVersion(string entryPointAddress);
}
```

`Latest` is v0.9. **v0.7 and v0.6 are not supported for signing**: this library signs the EIP-712 `userOpHash` introduced in EntryPoint v0.8, and `ValidateSupportedUserOpHashVersion` throws `NotSupportedException` for the two legacy keccak256 EntryPoints. Use `V08` or `V09`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;

// public record AADeploymentAddresses(
//     string EntryPointAddress, string NethereumAccountFactoryAddress,
//     string EcdsaValidatorAddress, string VerifyingPaymasterAddress);
var deploymentAddresses = new AADeploymentAddresses(
    EntryPointAddress: EntryPointAddresses.Latest,     // EntryPoint v0.9
    NethereumAccountFactoryAddress: "0x...",           // NethereumAccountFactory on your chain
    EcdsaValidatorAddress: "0x...",                    // ECDSAValidator module on your chain
    VerifyingPaymasterAddress: "");                    // optional, leave empty if unused

services.AddNethereumAccountAbstraction(o => o
    .UseWeb3(web3)
    .UseDeploymentAddresses(deploymentAddresses)
    .UseBundlerUrl("https://your-bundler-url"));       // or .UseBundler(myBundlerService)

// Elsewhere (e.g. constructor injection):
var aaClient = serviceProvider.GetRequiredService<IAAClient>();
```

`UseWeb3` can be omitted if an `IWeb3` is already registered in the container - the client resolves it from there instead.

### 2. The account model

`IAAClient` hands you one of two account types, both signed through the same `IAccountSigningService` seam (an offline `EthECKey`, or any external/hardware/WebAuthn signer):

```csharp
public class SmartAccount : AccountAbstractionAccount
{
    public bool IsDeployed { get; }
    public byte[]? Salt { get; }
    public byte[]? InitData { get; }

    public SmartAccount(
        string address, IAccountSigningService signingService,
        bool isDeployed, byte[]? salt = null, byte[]? initData = null);
}

public class NethereumSmartAccount : SmartAccount
{
    public IErc7579ValidatorModule Validator { get; }

    public NethereumSmartAccount(
        string address, IAccountSigningService signingService, IErc7579ValidatorModule validator,
        bool isDeployed, byte[]? salt = null, byte[]? initData = null);
}
```

`SmartAccount` is any ERC-4337 account shape (e.g. a plain `SimpleAccount`); `NethereumSmartAccount` adds the ERC-7579 validator module the modular `NethereumAccount` authenticates with.

The client itself is a small interface:

```csharp
public interface IAAClient
{
    Task<NethereumSmartAccount> CreateAccountAsync(EthECKey owner, byte[]? salt = null);

    Task<NethereumSmartAccount> CreateAccountAsync(
        IAccountSigningService signingService, IErc7579ValidatorModule validator,
        byte[] initData, byte[]? salt = null);

    NethereumSmartAccount GetAccount(
        string address, IAccountSigningService signingService, IErc7579ValidatorModule validator);

    SmartAccount GetAccount(string address, IAccountSigningService signingService);

    AAContractHandler Configure<TService>(TService service, NethereumSmartAccount account)
        where TService : ContractWeb3ServiceBase;

    AAContractHandler Configure<TService>(TService service, SmartAccount account)
        where TService : ContractWeb3ServiceBase;

    AAContractHandler ConfigureEip7702<TService>(
        TService service, NethereumSmartAccount account, EthECKey ownerKey,
        string accountImplementationAddress)
        where TService : ContractWeb3ServiceBase;
}
```

| You want to... | Call | Returns |
|---|---|---|
| create a brand-new modular account | `aaClient.CreateAccountAsync(ownerKey)` | `NethereumSmartAccount` (counterfactual, deploys on first op) |
| create a brand-new modular account for any signer | `aaClient.CreateAccountAsync(signingService, validator, initData)` | `NethereumSmartAccount` (counterfactual, deploys on first op) |
| attach to an already-deployed modular account | `aaClient.GetAccount(address, signingService, validator)` | `NethereumSmartAccount` |
| attach to an already-deployed plain account (e.g. `SimpleAccount`) | `aaClient.GetAccount(address, signingService)` | `SmartAccount` |

`aaClient.Configure(service, account)` then routes a generated typed contract service through Account Abstraction for that account - wiring the signer, the bundler, the EntryPoint, ERC-7579 execute encoding (for the modular tier) and, for a not-yet-deployed account, the factory that deploys it on the first op. Today `Configure` is the whole send pipeline (bundler-estimate "Simulate", then send "Execute", verified by the returned `AATransactionReceipt`).

### 2b. Any signer, not just an `EthECKey`

`CreateAccountAsync(ownerKey)` is a convenience wrapper for the ECDSA case. The underlying on-ramp is signer-agnostic: it takes any `IAccountSigningService` together with the `IErc7579ValidatorModule` that authenticates it, and the validator's own `initData` - the same shape works for a hardware/KMS/HSM/MPC signer or WebAuthn, with zero changes to `IAAClient` itself.

```csharp
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Signer;

// externalSigner is any IEthExternalSigner - a Key Vault/HSM/hardware-wallet signer whose key never
// lives in-process. Nethereum ships several (AWSKeyManagementExternalSigner, AzureKeyVaultExternalSigner,
// LedgerExternalSigner, TrezorExternalSigner, ...), each in its own package; write your own for anything
// else. (WebAuthn/passkeys plug in one level up, as a full IAccountSigningService of their own -
// WebAuthnAccountSigningService, shipped in Nethereum.WebAuthn - rather than as an IEthExternalSigner.)
IEthExternalSigner externalSigner = /* your Key Vault/HSM/hardware-wallet signer */;
var ownerAddress = await externalSigner.GetAddressAsync();

var signingService = new AccountSigningExternalService(externalSigner);
var validator = new EcdsaValidatorModule(deploymentAddresses.EcdsaValidatorAddress);
var initData = AccountInitDataBuilder.BuildEcdsa(deploymentAddresses.EcdsaValidatorAddress, ownerAddress);

var account = await aaClient.CreateAccountAsync(signingService, validator, initData);   // NethereumSmartAccount
counterService.UseAccountAbstraction(account, aaClient);
```

`AccountInitDataBuilder.Build`/`BuildEcdsa` encode the validator's own init payload (for `EcdsaValidatorModule`, `validator ‖ owner`); other validator modules (WebAuthn, MPC, session keys, ...) supply their own `IModuleConfig`-driven `initData` the same way. Everything downstream - counterfactual address derivation, deploy-on-first-op, `Configure` - is identical to the `EthECKey` tier above.

### 3. Modular account: create and send

`UseAccountAbstraction` and `Configure` are constrained `where TService : ContractWeb3ServiceBase`, so the service you pass must be a **code-generated** one — that base class is what the code generator emits. `counterService` below is the AA integration tests' generated `TestCounterService`; substitute your own generated service and its own `…RequestAndWaitForReceiptAsync` method. (For Nethereum's *built-in* standard services, which are not `ContractWeb3ServiceBase`, see [Built-in standard services](#built-in-standard-services) below.)

```csharp
using Nethereum.AccountAbstraction.Client;
using Nethereum.Signer;

var owner = EthECKey.GenerateKey();
var account = await aaClient.CreateAccountAsync(owner);   // NethereumSmartAccount, not deployed yet

// Fund the counterfactual address so it can pay for its own deployment + first UserOperation
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(account.Address, 0.1m);

counterService.UseAccountAbstraction(account, aaClient);   // switches ContractHandler to AAContractHandler

// First call deploys the account (via its counterfactual InitCode) and executes atomically
var receipt = (AATransactionReceipt)await counterService.CountRequestAndWaitForReceiptAsync();
Console.WriteLine($"UserOp Hash: {receipt.UserOpHash}, Success: {receipt.UserOpSuccess}");
```

### 4. Non-modular account: attach to an already-deployed account

For a plain, already-deployed ERC-4337 account (e.g. a `SimpleAccount`) that has no ERC-7579 validator:

```csharp
using Nethereum.Accounts.AccountMessageSigning;

var signingService = new AccountSigningOfflineService(ownerKey);
var account = aaClient.GetAccount(existingAccountAddress, signingService);   // SmartAccount

var handler = aaClient.Configure(counterService, account);
var receipt = (AATransactionReceipt)await counterService.CountRequestAndWaitForReceiptAsync();
```

`Configure` returns the `AAContractHandler` directly, which is how you reach the fluent configuration (paymaster, gas, batching) covered below - `UseAccountAbstraction` in step 3 is a shortcut for `aaClient.Configure(service, account)` that hands the service straight back for the common case where you don't need the handler itself.

### 5. EIP-7702: upgrade an existing EOA in place

The tiers above create a brand-new address (CREATE2, counterfactual). EIP-7702 instead turns an **existing, already-funded EOA** into a modular smart account at its own address: its very first UserOperation carries a signed EIP-7702 authorisation tuple that delegates the EOA to `accountImplementationAddress` (the same `NethereumAccountFactory.accountImplementation` every counterfactual account already runs), then runs `initializeAccount` as the post-delegation initializer to install the validator - no factory/CREATE2 deploy involved, same address before and after.

```csharp
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.Signer;

var ownerKey = new EthECKey("your-existing-eoa-private-key");   // already funded, already has a nonce

// CreateEip7702Account is an extension method on IAAClient, from Eip7702AAClientExtensions
// (namespace Nethereum.AccountAbstraction.Client) - not a member of the interface itself.
var account = aaClient.CreateEip7702Account(ownerKey, deploymentAddresses.EcdsaValidatorAddress);

var factory = new NethereumAccountFactoryService(web3, deploymentAddresses.NethereumAccountFactoryAddress);
var accountImplementation = await factory.AccountImplementationQueryAsync();

var handler = aaClient.ConfigureEip7702(counterService, account, ownerKey, accountImplementation);

// This one UserOperation both delegates the EOA and installs the ECDSA validator (ownerKey as owner),
// then executes - atomically, at the EOA's own unchanged address.
var receipt = (AATransactionReceipt)await counterService.CountRequestAndWaitForReceiptAsync();
Console.WriteLine($"Success: {receipt.UserOpSuccess}");
```

After that op, `eth_getCode` on `ownerKey`'s address returns the EIP-7702 delegation designator (`0xef0100` ‖ `accountImplementation`, 23 bytes) instead of empty bytecode: the EOA is now a modular `NethereumAccount` at the same address, driven exactly like the counterfactual tier from then on. `ownerKey` signs both the UserOperation (as `account`'s owner) and the 7702 authorisation tuple, so it must be a raw `EthECKey` - the authorisation tuple is secp256k1-only, unlike the fully signer-agnostic counterfactual tier in 2b.

## Batching Multiple Calls

Execute multiple contract calls in a single UserOperation. All calls target the handler's contract. A batch entry is a `BatchCall`:

```csharp
public class BatchCall
{
    public byte[] CallData { get; set; }
    public BigInteger Value { get; set; }

    public BatchCall();
    public BatchCall(byte[] callData, BigInteger value = default);

    public static BatchCall From<TFunctionMessage>(TFunctionMessage message, BigInteger? ethValue = null)
        where TFunctionMessage : FunctionMessage;
}

public static class BatchCallExtensions
{
    public static BatchCall ToBatchCall<TFunctionMessage>(this TFunctionMessage message, BigInteger? ethValue = null)
        where TFunctionMessage : FunctionMessage;
}
```

`ToBatchCall()` with no `ethValue` carries the message's own `AmountToSend`.

Every routing call hands you the handler: `aaClient.Configure(generatedService, account)` for a code-generated service, `generatedService.ChangeContractHandlerToAA(...)` at the low level, or `erc20.SwitchToAccountAbstraction(...)` for a built-in standard service.

```csharp
var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
var handler = erc20.SwitchToAccountAbstraction(
    accountAddress, ownerKey, bundlerService, entryPointAddress, factory: factoryConfig);

// Use ToBatchCall() extension method for clean, type-safe batching
var receipt = await handler.BatchExecuteAsync(
    new TransferFunction { To = addr1, Value = 100 }.ToBatchCall(),
    new TransferFunction { To = addr2, Value = 200 }.ToBatchCall(),
    new TransferFunction { To = addr3, Value = 300 }.ToBatchCall());

// All three operations succeed or fail atomically
if (receipt.UserOpSuccess)
{
    Console.WriteLine("All transfers completed!");
}
```

### Batch API Options

```csharp
// Recommended: Use ToBatchCall() for type safety
await handler.BatchExecuteAsync(
    new TransferFunction { To = addr1, Value = 100 }.ToBatchCall(),
    new ApproveFunction { Spender = spender, Value = 500 }.ToBatchCall());

// With ETH value: ToBatchCall(ethValue)
await handler.BatchExecuteAsync(
    new DepositFunction().ToBatchCall(ethValue: Web3.Convert.ToWei(1)));

// Simple: Just raw call data bytes
await handler.BatchExecuteAsync(callData1, callData2, callData3);

// Generic: Pass FunctionMessage objects directly (all same type)
await handler.BatchExecuteAsync(
    new CountFunction(),
    new CountFunction(),
    new CountFunction());
```

## Gas Sponsorship with Paymasters

Paymasters can sponsor gas costs for your users. `AAContractHandler.WithPaymaster` takes either an address (plus optional static data) or a `PaymasterConfig`:

```csharp
public class PaymasterConfig
{
    public string Address { get; set; }
    public byte[] Data { get; set; }
    public Func<UserOperation, Task<byte[]>> DataProvider { get; set; }

    public PaymasterConfig();
    public PaymasterConfig(string address, byte[] data = null);
    public PaymasterConfig(string address, Func<UserOperation, Task<byte[]>> dataProvider);
}
```

```csharp
handler.WithPaymaster(paymasterAddress);

// Or with custom paymaster data (e.g., for verifying paymasters)
handler.WithPaymaster(paymasterAddress, paymasterData);

// Or with dynamic, per-operation paymaster data
handler.WithPaymaster(new PaymasterConfig(
    paymasterAddress,
    dataProvider: async (userOp) => {
        // Generate signed paymaster data based on the UserOperation
        return await GetSignedPaymasterData(userOp);
    }));
```

### Driving a paymaster contract: `IPaymasterManager`

`WithPaymaster` only attaches paymaster data to your operation. To *run* a paymaster - fund its EntryPoint deposit, sign sponsorships, top up a user's allowance - the `Nethereum.AccountAbstraction.Paymasters` namespace has typed managers:

```csharp
public interface IPaymasterManager
{
    string Address { get; }
    string EntryPointAddress { get; }

    Task<SponsorResult> SponsorUserOperationAsync(PackedUserOperation userOp, SponsorContext? context = null);
    Task<BigInteger> GetDepositAsync();
    Task<TransactionReceipt> DepositAsync(BigInteger amount);
    Task<TransactionReceipt> WithdrawToAsync(string to, BigInteger amount);
}

public interface IVerifyingPaymasterManager : IPaymasterManager
{
    Task<SponsorResult> SponsorWithSignatureAsync(
        PackedUserOperation userOp, ulong validUntil, ulong validAfter, EthECKey signerKey);
    Task<byte[]> GetHashAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter);
}

public interface IDepositPaymasterManager : IPaymasterManager
{
    Task<BigInteger> GetUserDepositAsync(string account);
    Task<TransactionReceipt> DepositForAsync(string account, BigInteger amount);
    Task<TransactionReceipt> WithdrawFromAsync(string account, BigInteger amount);
}

public interface ITokenPaymasterManager : IPaymasterManager
{
    Task<string> GetTokenAddressAsync();
    Task<BigInteger> EstimateTokenCostAsync(BigInteger ethCost);
    Task<BigInteger> GetTokenBalanceAsync(string account);
}
```

`VerifyingPaymasterManager` (a sponsor signs each op off-chain) and `DepositPaymasterManager` (each user pre-funds their own sponsorship balance) ship in the box; both are loaded through the static `LoadAsync`, which queries the paymaster contract's `entryPoint()` **on-chain** before handing the manager back — so it is an async round trip, and constructing a manager directly with `new` leaves `EntryPointAddress` throwing `InvalidOperationException` ("EntryPoint not loaded. Call LoadAsync first."). `IWeb3` extension shortcuts do the same thing:

```csharp
var verifying = await web3.GetVerifyingPaymasterAsync(paymasterAddress, sponsorKey);
var deposit   = await web3.GetDepositPaymasterAsync(paymasterAddress);
```

A sponsorship attempt answers with a `SponsorResult`, never an exception - `IsSponsored` plus the `PaymasterAndData` to attach, or `Error` explaining the refusal:

```csharp
public class SponsorResult
{
    public bool IsSponsored { get; set; }
    public byte[] PaymasterAndData { get; set; } = Array.Empty<byte>();
    public string? PaymasterAddress { get; set; }
    public ulong ValidAfter { get; set; }
    public ulong ValidUntil { get; set; }
    public BigInteger EstimatedGasCost { get; set; }
    public string? Error { get; set; }

    public static SponsorResult Success(
        byte[] paymasterAndData, string paymasterAddress, ulong validUntil = 0, ulong validAfter = 0);
    public static SponsorResult Failure(string error);
}

public class SponsorContext
{
    public string? SenderAddress { get; set; }
    public BigInteger? MaxGasPrice { get; set; }
    public ulong? ValidUntil { get; set; }
    public ulong? ValidAfter { get; set; }
    public string? Metadata { get; set; }
}
```

## Configuration Options

### Gas and Timeout Settings

```csharp
public class AAGasConfig
{
    public BigInteger? VerificationGasBuffer { get; set; }
    public BigInteger? CallGasBuffer { get; set; }
    public BigInteger? PreVerificationGasBuffer { get; set; }
    public decimal CallGasMultiplier { get; set; } = 1.0m;
    public decimal VerificationGasMultiplier { get; set; } = 1.0m;
    public int ReceiptPollIntervalMs { get; set; } = 1000;
    public int ReceiptTimeoutMs { get; set; } = 60000;

    public static AAGasConfig Default { get; }
}
```

`AAContractHandler.GasConfig` starts as `AAGasConfig.Default`, which sets only `PreVerificationGasBuffer = 1000` - every other buffer is unset and both multipliers are `1.0m`. Override what you need (the multipliers are `decimal`, so the literals need the `m` suffix):

```csharp
handler.WithGasConfig(new AAGasConfig
{
    ReceiptPollIntervalMs = 1000,     // How often to check for receipt
    ReceiptTimeoutMs = 60000,         // Max wait time for mining
    VerificationGasBuffer = 5000,     // Extra gas buffer for verification
    CallGasBuffer = 10000,            // Extra gas buffer for call execution
    PreVerificationGasBuffer = 2000,  // Extra gas buffer for pre-verification
    CallGasMultiplier = 1.2m,         // Multiplier for call gas estimation
    VerificationGasMultiplier = 1.5m  // Multiplier for verification gas estimation
});
```

### Fluent Configuration

`AAContractHandler` is a `ContractHandler` whose configuration methods return the handler, allowing chaining, regardless of whether it came from `IAAClient.Configure` or the low-level `ChangeContractHandlerToAA` (see Advanced section below):

```csharp
public class AAContractHandler : ContractHandler
{
    public FactoryConfig FactoryConfig { get; private set; }
    public PaymasterConfig PaymasterConfig { get; private set; }
    public Eip7702DelegationConfig Eip7702DelegationConfig { get; private set; }
    public AAGasConfig GasConfig { get; private set; } = AAGasConfig.Default;
    public string AccountAddress { get; }
    public string EntryPointAddress { get; }

    public AAContractHandler WithFactory(FactoryConfig config);
    public AAContractHandler WithFactory(IAccountInitCodeBuilder initCodeBuilder);
    public AAContractHandler WithPaymaster(string paymasterAddress, byte[] paymasterData = null);
    public AAContractHandler WithPaymaster(PaymasterConfig config);
    public AAContractHandler WithEip7702Delegation(string delegateAddress, byte[] factoryData = null);
    public AAContractHandler WithGasConfig(AAGasConfig config);
    public AAContractHandler WithExecuteEncoder(IExecuteEncoder executeEncoder);
    public AAContractHandler WithErc7579Execution();

    public Task<AATransactionReceipt> BatchExecuteAsync(params BatchCall[] calls);
    public Task<AATransactionReceipt> BatchExecuteAsync(params byte[][] callDatas);
    public Task<AATransactionReceipt> BatchExecuteAsync<TFunctionMessage>(
        params TFunctionMessage[] functionMessages) where TFunctionMessage : FunctionMessage;

    public Task<PackedUserOperation> CreateUserOperationAsync<TEthereumContractFunctionMessage>(
        TEthereumContractFunctionMessage transactionMessage)
        where TEthereumContractFunctionMessage : FunctionMessage, new();
}
```

It overrides four base `ContractHandler` members. Three keep an existing typed service working unchanged: `SendRequestAndWaitForReceiptAsync` (returning an `AATransactionReceipt`), `SendRequestAsync` (returning the userOp hash) and `EstimateGasAsync` (the sum of `callGasLimit + verificationGasLimit + preVerificationGas`).

The fourth, **`SignTransactionAsync`, throws `NotSupportedException`** — a UserOperation has no raw transaction encoding to sign. Build and sign the operation with `CreateUserOperationAsync` and submit it through the bundler service instead. Any code path that reached for a signed raw transaction has to change when you switch a service to AA.

`WithExecuteEncoder` chooses how a call is wrapped for the account: `SimpleAccountExecuteEncoder` (the plain `execute(target, value, data)` shape) or `Erc7579ExecuteEncoder` for the modular tier - `WithErc7579Execution()` is the shortcut for the latter.


```csharp
var handler = aaClient.Configure(counterService, account)
    .WithPaymaster(paymasterAddress)
    .WithGasConfig(gasConfig);
```

## Understanding the Receipt

`AATransactionReceipt` extends the standard `TransactionReceipt` (which carries the *bundle transaction's* fields) with the UserOperation's own outcome, plus three derived members that turn that outcome into a diagnosis:

```csharp
public enum UserOperationFailureKind
{
    Succeeded,
    RevertedWithReason,
    FailedWithoutReason
}

public class AATransactionReceipt : TransactionReceipt
{
    public string UserOpHash { get; set; }
    public bool UserOpSuccess { get; set; }
    public string RevertReason { get; set; }
    public BigInteger ActualGasCost { get; set; }
    public BigInteger ActualGasUsed { get; set; }
    public string Paymaster { get; set; }
    public string Sender { get; set; }

    public UserOperationFailureKind FailureKind { get; }
    public bool IsLikelyOutOfGas { get; }
    public string FailureDiagnostic { get; }

    public static AATransactionReceipt FromUserOperationReceipt(UserOperationReceipt userOpReceipt);
}
```

```csharp
var receipt = (AATransactionReceipt)await erc20.TransferRequestAndWaitForReceiptAsync(recipient, amount);

// Standard transaction fields (from the bundle transaction)
Console.WriteLine($"Block: {receipt.BlockNumber}");
Console.WriteLine($"Tx Hash: {receipt.TransactionHash}");

// AA-specific fields
Console.WriteLine($"UserOp Hash: {receipt.UserOpHash}");
Console.WriteLine($"Success: {receipt.UserOpSuccess}");
Console.WriteLine($"Revert Reason: {receipt.RevertReason}");
Console.WriteLine($"Actual Gas Used: {receipt.ActualGasUsed}");
Console.WriteLine($"Actual Gas Cost: {receipt.ActualGasCost}");
Console.WriteLine($"Sender: {receipt.Sender}");
Console.WriteLine($"Paymaster: {receipt.Paymaster}");
```

### Diagnosing a failure

A UserOperation that reverted *with* a reason and one that ran out of gas look almost identical on the receipt - both are `UserOpSuccess == false`. `FailureKind` separates them, treating an empty reason and the bare placeholder `"execution reverted"` as "no reason at all"; `FailureDiagnostic` puts that into words, and `IsLikelyOutOfGas` is the shorthand.

Extracted from `AATransactionReceiptDiagnosticTests` (`tests/Nethereum.AccountAbstraction.UnitTests/AATransactionReceiptDiagnosticTests.cs`):

```csharp
// Succeeded: FailureKind == Succeeded, IsLikelyOutOfGas == false, FailureDiagnostic == null
var ok = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(true, null));

// Failed with no reason: the shape of an out-of-gas failure in the account call
var oog = AATransactionReceipt.FromUserOperationReceipt(BuildReceipt(false, null));
// oog.FailureKind == UserOperationFailureKind.FailedWithoutReason
// oog.IsLikelyOutOfGas == true
// oog.FailureDiagnostic mentions callGasLimit
```

A bare `revert()` with no message is indistinguishable from an out-of-gas failure from the receipt alone, which is why `IsLikelyOutOfGas` is named *likely*.

## Inspecting UserOperations

You can inspect a UserOperation before sending it:

```csharp
// Create but don't send
var packedOp = await handler.CreateUserOperationAsync(
    new TransferFunction { To = recipient, Value = amount });

Console.WriteLine($"Sender: {packedOp.Sender}");
Console.WriteLine($"Nonce: {packedOp.Nonce}");
Console.WriteLine($"InitCode length: {packedOp.InitCode?.Length ?? 0}");
Console.WriteLine($"CallData: {packedOp.CallData.ToHex()}");
```

## Estimating Gas

```csharp
// Estimate total gas for a UserOperation
var gas = await erc20.ContractHandler.EstimateGasAsync<TransferFunction>(
    new TransferFunction { To = recipient, Value = amount });

Console.WriteLine($"Estimated gas: {gas.Value}");
// This includes: verificationGasLimit + callGasLimit + preVerificationGas
```

## Error Handling

```csharp
try
{
    var receipt = (AATransactionReceipt)await erc20.TransferRequestAndWaitForReceiptAsync(recipient, amount);

    if (!receipt.UserOpSuccess)
    {
        // UserOp was included but inner execution failed
        Console.WriteLine($"Execution failed: {receipt.RevertReason}");
    }
}
catch (TimeoutException ex)
{
    // UserOp wasn't mined within the timeout period
    Console.WriteLine($"Timeout waiting for UserOp: {ex.Message}");
}
catch (RpcClientUnknownException ex)
{
    // Bundler rejected the UserOp or connection failed
    Console.WriteLine($"Bundler error: {ex.Message}");
}
```

## Advanced / Low-Level: Manual Account Management

`IAAClient` covers the common on-ramp for a fresh or already-deployed `NethereumAccount`/`SimpleAccount`. For anything else - a raw key with no DI container, a custom factory, or driving `AAContractHandler` directly - `ChangeContractHandlerToAA` and `AAContractHandler` remain the underlying building blocks `IAAClient` itself is built on.

### Switching an existing contract service to AA directly

```csharp
using Nethereum.AccountAbstraction;
using Nethereum.Signer;

var ownerKey = new EthECKey("your-private-key");
var bundlerService = new AccountAbstractionBundlerService(
    new RpcClient(new Uri("https://your-bundler-url")));
var entryPointAddress = EntryPointAddresses.Latest; // v0.9

// Switch to Account Abstraction - one line!
// counterService is a code-generated service (ContractWeb3ServiceBase), which is what
// ChangeContractHandlerToAA is constrained to.
counterService.ChangeContractHandlerToAA(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress);

// Now all transactions go through UserOperations
var receipt = (AATransactionReceipt)await counterService.CountRequestAndWaitForReceiptAsync();
Console.WriteLine($"UserOp Hash: {receipt.UserOpHash}");
Console.WriteLine($"Success: {receipt.UserOpSuccess}");
```

`ChangeContractHandlerToAA` also has overloads that take a standard `IAccount` (its `AccountSigningService` becomes the UserOperation signer) or an explicit `IAccountSigningService` + `IErc7579ValidatorModule?` pair - the same seam `IAAClient` uses internally. All three are constrained `where T : ContractWeb3ServiceBase`.

### Auto-Deploying via a `FactoryConfig`

If your smart account doesn't exist yet, have it deployed automatically on the first transaction. `FactoryConfig` is what the handler turns into the operation's `initCode`:

```csharp
public class FactoryConfig
{
    public string FactoryAddress { get; set; }
    public string Owner { get; set; }
    public BigInteger Salt { get; set; } = 0;

    public FactoryConfig();
    public FactoryConfig(string factoryAddress, string owner, BigInteger? salt = null);
}
```


`SimpleAccountFactoryService` lives in the separate **Nethereum.AccountAbstraction.SimpleAccount** package (`dotnet add package Nethereum.AccountAbstraction.SimpleAccount`) — it is not part of `Nethereum.AccountAbstraction`.

```csharp
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;

// Deploy a factory and calculate the account address (it doesn't exist yet)
var factory = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
    web3, new SimpleAccountFactoryDeployment { EntryPoint = entryPointAddress });

var accountAddress = await factory.GetAddressQueryAsync(ownerKey.GetPublicAddress(), salt: 0);

// Fund the address so it can pay for deployment + first transaction
await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync(accountAddress, 0.1m);

// Switch to AA with a factory config
counterService.ChangeContractHandlerToAA(
    accountAddress,
    ownerKey,
    bundlerService,
    entryPointAddress,
    factory: new FactoryConfig(factory.ContractAddress, ownerKey.GetPublicAddress(), salt: 0));

// First transaction will:
// 1. Deploy the smart account (via initCode)
// 2. Execute your contract call
var receipt = await counterService.CountRequestAndWaitForReceiptAsync();
```

The handler automatically checks if the account exists. If not, it includes the `initCode` to deploy it. On subsequent calls, `initCode` is omitted.

## Supported Contract Services

There are **two entry points, and which one compiles depends on the service's base type.**

| Your service is... | Route it with | Constraint |
|---|---|---|
| **code-generated** (extends `ContractWeb3ServiceBase`) | `IAAClient.Configure` / `ConfigureEip7702`, `UseAccountAbstraction`, or `ChangeContractHandlerToAA` | `where T : ContractWeb3ServiceBase` |
| a **built-in standard service** (implements `IContractHandlerService` only) | `SwitchToAccountAbstraction` | `where T : IContractHandlerService` |

`ContractWeb3ServiceBase` itself implements `IContractHandlerService`, so a generated service can use either. The reverse does not hold: the built-in standard services below do **not** extend `ContractWeb3ServiceBase`, so `ChangeContractHandlerToAA` / `Configure` / `UseAccountAbstraction` will not bind to them. `SwitchToAccountAbstraction` is the one that does.

### Built-in standard services

Each of these implements `IContractHandlerService` only, so `SwitchToAccountAbstraction` is their route:

| Service | Namespace |
|---------|-----------|
| `ERC20ContractService` | `Nethereum.Contracts.Standards.ERC20` |
| `ERC721ContractService` | `Nethereum.Contracts.Standards.ERC721` |
| `ERC1155ContractService` | `Nethereum.Contracts.Standards.ERC1155` |
| `ERC1271ContractService` | `Nethereum.Contracts.Standards.ERC1271` |
| `ERC165SupportsInterfaceContractService` | `Nethereum.Contracts.Standards.ERC165` |
| `EIP3009ContractService` | `Nethereum.Contracts.Standards.EIP3009` |
| `ENSRegistryService` | `Nethereum.Contracts.Standards.ENS` |
| `ETHRegistrarControllerService` | `Nethereum.Contracts.Standards.ENS` |
| `PublicResolverService` | `Nethereum.Contracts.Standards.ENS` |
| `OffchainResolverService` | `Nethereum.Contracts.Standards.ENS` |
| `RegistrarService` | `Nethereum.Contracts.Standards.ENS` |
| `Permit2Service` | `Nethereum.Contracts.Standards.Permit2` |

### `SwitchToAccountAbstraction`

```csharp
public static class AAContractHandlerExtensions
{
    public static AAContractHandler SwitchToAccountAbstraction<T>(
        this T service,
        string accountAddress,
        EthECKey signerKey,
        IAccountAbstractionBundlerService bundlerService,
        string entryPointAddress,
        FactoryConfig factory = null)
        where T : IContractHandlerService;
}
```

One overload only — an `EthECKey` signer. It rebuilds the service's existing `ContractHandler` as an `AAContractHandler`, assigns it back, and returns it, so the fluent knobs (`WithPaymaster`, `WithGasConfig`, `BatchExecuteAsync`) are all reachable from the return value.

```csharp
using Nethereum.AccountAbstraction;

var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);

erc20.SwitchToAccountAbstraction(
    accountAddress,
    accountKey,
    bundlerService,
    entryPointAddress,
    factory: factoryConfig);

// Every write now goes out as a UserOperation from accountAddress
var receipt = (AATransactionReceipt)await erc20.TransferRequestAndWaitForReceiptAsync(
    recipient, transferAmount);

// Reads are unaffected - queries still go out as eth_call
var balance = await erc20.BalanceOfQueryAsync(recipient);
```

## Authorization Rules (on-chain rule engine)

Beyond a single session policy, Nethereum ships a small **rule engine** for composing on-chain authorization: reusable **rules** are registered in a per-deployment **registry** and wired by a **combinator** into a SmartSession action policy. It lets you express "this session key may act **only within these limits**, authorised by **N-of-M** signers" — enforced on-chain during validation, with no off-chain trust.

### The pieces

| Piece | Contract | Role |
|---|---|---|
| Rule | `IRule` (`evaluate(bytes) → bytes`) | a self-contained, **pure** decision (`ValueCapRule` = `value ≤ cap`). No storage reads, so it is ERC-7562 safe. |
| Registry | `RuleRegistry` | the per-deployment **trusted set**: `registerRule(id, rule)` → `closeRegistration()` (irreversible freeze). Ids are immutable once set. |
| Combinator | `CombinatorBase` + e.g. `ValueCapCombinator` | a SmartSession `IActionPolicy` that resolves a rule id to its address **at install** and evaluates it during validation. |
| Authority | `OwnableValidator` (reused) | N-of-M `{threshold, owners}` as the session validator — the *who*; the combinator is the *what*. |

### Deploy → register → freeze

```csharp
// 1. registry + a rule, then register it under a stable id
var registry = await RuleRegistryService.DeployContractAndGetServiceAsync(web3, new RuleRegistryDeployment());
var capRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(web3, new ValueCapRuleDeployment());
var capRuleId = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("value-cap"));
await registry.RegisterRuleRequestAndWaitForReceiptAsync(capRuleId, capRuleReceipt.ContractAddress);

// 2. a combinator bound to that registry (the trusted rule set for this deployment)
var combinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
    web3, new ValueCapCombinatorDeployment { Registry = registry.ContractAddress });

// 3. once every rule is registered, freeze the set - no rule id can ever be added or retargeted again
await registry.CloseRegistrationRequestAndWaitForReceiptAsync();
```

### Wire it into a session (authority × cap)

The combinator plugs in as an ordinary SmartSession action policy; its config is `abi.encode(capRuleId, cap)`. Pair it with the `OwnableValidator` contract as the session validator for the N-of-M authority (in C# that is `OwnableValidatorService` to drive the contract, `OwnableValidatorConfig` to build its init data — there is no C# type simply called `OwnableValidator`):

```csharp
var capConfig = new ABIEncode().GetABIEncoded(
    new ABIValue("bytes32", capRuleId),
    new ABIValue("uint256", (BigInteger)100));   // per-action cap

var salt = new byte[32];                                          // 32-byte salt, unique per session
salt[31] = 1;

var session = new SmartSessionConfig()
    .WithSessionValidator(ownableValidatorAddress)                 // N-of-M authority ({threshold, owners})
    .WithSessionValidatorInitData(ownableValidatorInitData)
    .WithSalt(salt)
    .WithAction(new ActionDataBuilder()
        .WithTarget(targetAddress)
        .WithSelector(selector)
        .WithPolicy(combinator.ContractAddress, capConfig)         // the cap rule, via the combinator
        .Build());
```

`SmartSessionConfig.GetInitData()` emits `installMode ‖ abi.encode(Session[])`, and `InstallMode` defaults to `SmartSessionMode.UnsafeEnable`. Set it explicitly so the mode you install under is a decision, not a default:

```csharp
session.WithInstallMode(SmartSessionMode.Enable);   // or .UnsafeEnable, deliberately
```

**Install this data through the SmartSession module's `onInstall`** — i.e. as module install data during account deployment, or via an `installModule` call — which is the execution-phase path the section above requires. `onInstall` accepts only an enable mode (`Enable` or `UnsafeEnable`) and rejects `Use`. Then let the paying operations run in **USE mode**. Do *not* enable a registry-backed combinator lazily from `validateUserOp` unless the sender is staked.

An in-quorum op within the cap executes; an under-quorum op is rejected by the authority; an over-cap op is rejected by the combinator — all on-chain during validation.

### ERC-7562 safety is conditional — enable in EXECUTION, pay in USE mode

**This is not unconditional.** Resolving a rule id through the registry is a plain `SLOAD` of `registry.ruleOf[ruleId]` — a slot keyed by the rule id, **not** by the account address, so it is *not* ERC-7562 sender-associated. An unstaked entity that reads it **during validation** is rejected by a 7562-enforcing bundler.

The design keeps that read out of validation, and it only works if you enable the session the right way:

| | |
|---|---|
| **Safe** | Enable the session/policy through an **execution-phase** path — the SmartSession module's `onInstall`, or a prior enabling transaction — so `initializeWithMultiplexer` resolves the rule id once, in execution, and caches `{rule, cap}` in a fixed-layout struct at `_config[id][msg.sender][account]`. Then pay in **USE mode** (`mode ‖ permissionId ‖ blob`), whose validation reads only that cached struct and calls the pure rule directly. `checkAction` never touches the registry, so an **unstaked** account is fine. |
| **Not safe** | Enabling a registry-backed combinator lazily through `SmartSession.validateUserOp` in **ENABLE / UNSAFE_ENABLE** mode. That routes `initializeWithMultiplexer` — and its registry `SLOAD` — into the **validation** frame, which a 7562-enforcing bundler rejects for an unstaked sender. A caller that needs validateUserOp-ENABLE-mode installs of a registry-backed combinator **must stake the sender**. |

(Because rule ids are immutable once registered, the cached address can never go stale.)

The condition is stated on the contracts themselves — see the `@dev` notes on `CombinatorBase` and `ValueCapCombinator`.

### Authoring your own rule

1. Write an `IRule` whose `evaluate` is **pure and gas-bounded** regardless of its input, and returns `abi.encode(...)` (a 32-byte `bool` for a yes/no rule).
2. Register it in a `RuleRegistry`.
3. Either reuse `ValueCapCombinator` (for a cap-shaped decision) or write a small combinator extending `CombinatorBase` — resolve the rule id in `initializeWithMultiplexer`, cache what you need in a fixed struct, and evaluate it in `checkAction`. **Never read the registry from `checkAction`** (it would break ERC-7562 validation).

> **Security:** `ValueCapRule`/`ValueCapCombinator` enforce a **per-action** ceiling, not a cumulative budget — they keep no running total, so a batched userOp can move up to *N × cap*. For a cumulative/session spending limit, use a spending-limit-style rule that tracks a persistent `used` amount per account.

## Session Keys

`SessionKeys/` generates and tracks temporary signing keys scoped to an account, independent of any on-chain module.

```csharp
public class SessionKeyManager
{
    public SessionKeyManager(ISessionKeyStore? store = null);

    public Task<GeneratedSessionKey> GenerateSessionKeyAsync(string accountAddress, int validDays = 30);
    public Task<SessionKeyEntry?> GetSessionKeyAsync(string keyAddress);
    public Task<SessionKeyEntry[]> GetSessionKeysForAccountAsync(string accountAddress);
    public Task MarkRegisteredAsync(string keyAddress);
    public Task RemoveSessionKeyAsync(string keyAddress);
    public Task<SessionKeyEntry?> GetBestSessionKeyAsync(string accountAddress);
}
```

`GenerateSessionKeyAsync` creates a new `EthECKey`, records it as an inactive `SessionKeyEntry` (valid from now until `validDays` later), and persists it through the store; `MarkRegisteredAsync` is what an application calls once it has actually installed/enabled that key on-chain, flipping `IsActive` to `true`. `GetBestSessionKeyAsync` returns the active key with the latest `ValidUntil` that is currently within its `ValidAfter`/`ValidUntil` window.

```csharp
public interface ISessionKeyStore
{
    Task SaveAsync(SessionKeyEntry entry);
    Task<SessionKeyEntry?> LoadAsync(string keyAddress);
    Task<SessionKeyEntry[]> LoadAllAsync();
    Task DeleteAsync(string keyAddress);
}

public class InMemorySessionKeyStore : ISessionKeyStore { }
```

`InMemorySessionKeyStore` is the default backing store `SessionKeyManager` uses when none is supplied — process-lifetime only; a real application supplies its own `ISessionKeyStore` for durable storage.

```csharp
public class SessionKeyEntry
{
    public string Key { get; set; }
    public string PrivateKey { get; set; }
    public string AccountAddress { get; set; }
    public ulong ValidAfter { get; set; }
    public ulong ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }

    public bool IsValidAt(ulong timestamp);
    public bool IsValidNow();
}
```

## Gas Estimation Internals

`GasEstimation/` is the library the bundler-facing `Nethereum.AccountAbstraction.Bundler` package builds its own estimator on top of.

```csharp
public interface IEvmGasEstimator
{
    Task<EvmEstimationResult> EstimateGasAsync(string from, string to, byte[] data, BigInteger value, long gasLimit);
}

public class UserOperationGasEstimator
{
    public UserOperationGasEstimator(IWeb3 web3, string entryPointAddress, string bundlerAddress = null);
    public UserOperationGasEstimator(IEvmGasEstimator evmEstimator, string entryPointAddress, string bundlerAddress = null);
    public UserOperationGasEstimator(IWeb3 web3, IEvmGasEstimator evmEstimator, string entryPointAddress, string bundlerAddress = null);

    public Task<UserOperationGasEstimateResult> EstimateGasAsync(UserOperation userOp);
    public Task<BigInteger> EstimateCallGasAsync(UserOperation userOp);
    public BigInteger CalculatePreVerificationGas(UserOperation userOp);
    public static BigInteger CalculateCalldataCost(byte[] data);
    public static PackedUserOperation PackUserOperationForGasEstimate(UserOperation userOp);
}
```

`UserOperationGasEstimator` has two estimation backends behind the same result shape: an `IWeb3` node (`eth_estimateGas` round-trips) or an in-process `IEvmGasEstimator` — the third constructor accepts both and prefers the node path where each estimation step supports it. `EstimateGasAsync` first tries estimating verification gas via a real `handleOps` simulation (`TryEstimateViaHandleOpsAsync`); on failure it falls back to `EstimateVerificationGasLegacyAsync`, a heuristic based on whether the op deploys a new account (`HasInitCode`) and/or uses a paymaster (`HasPaymaster`).

```csharp
public class UserOperationGasEstimateResult
{
    public BigInteger PreVerificationGas { get; set; }
    public BigInteger VerificationGasLimit { get; set; }
    public BigInteger CallGasLimit { get; set; }
    public BigInteger MaxFeePerGas { get; set; }
    public BigInteger MaxPriorityFeePerGas { get; set; }
    public BigInteger PaymasterVerificationGasLimit { get; set; }
    public BigInteger PaymasterPostOpGasLimit { get; set; }
}
```

`GasEstimationConstants` collects every magic number the estimator uses — calldata byte costs, overhead constants, and the paymaster/verification fallback values used when a real estimate isn't available:

```csharp
public static class GasEstimationConstants
{
    public const int ZERO_BYTE_GAS_COST = 4;
    public const int NON_ZERO_BYTE_GAS_COST = 16;
    public const int PRE_VERIFICATION_OVERHEAD_GAS = 50000;
    public const int BASE_TRANSACTION_GAS = 21000;
    public const int VERIFICATION_GAS_BUFFER = 100000;
    public const int PAYMASTER_VALIDATION_GAS_BUFFER = 20000;
    public const int MAX_VERIFICATION_GAS = 500000;
    public const int DEFAULT_CALL_GAS_LIMIT = 100000;
    public const int INNER_GAS_OVERHEAD = 10000;
    public const int PER_USER_OP_WORD_GAS = 8;
    public const int PER_USER_OP_OVERHEAD = 18300;
    public const int SIGNATURE_SIZE = 65;
    public const int ADDRESS_SIZE = 20;
    public const int WORD_SIZE = 32;
    public static readonly BigInteger FIXED_VERIFICATION_GAS_OVERHEAD = 21000;
    public static readonly BigInteger ACCOUNT_DEPLOYMENT_BASE_GAS = 32000;
    public static readonly BigInteger CREATE2_COST = ACCOUNT_DEPLOYMENT_BASE_GAS;
    public const int HANDLE_OPS_FIXED_OVERHEAD = 30000;
    public const int VERIFICATION_GAS_BUFFER_PERCENT = 20;
    public const int CALL_GAS_BUFFER_PERCENT = 20;
    public const long MAX_SIMULATION_GAS = 10_000_000;
    public const int DEFAULT_VERIFICATION_GAS_FALLBACK = 150000;
    public const int DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK = 100000;
    public const int DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK = 50000;
}
```

## Governance

`Governance/` provides an EIP-712 multi-signature workflow for changing a smart account factory's configuration (registering/unregistering modules, updating the admin set) without a single key controlling it.

```csharp
public class FactoryGovernanceSigner
{
    public FactoryGovernanceSigner(BigInteger chainId, string verifyingContract);

    public Domain GetDomain();
    public TypedData<Domain> GetTypedDefinition<T>();
    public string SignRegisterModule(RegisterModuleMessage message, string privateKey);
    public string SignUnregisterModule(UnregisterModuleMessage message, string privateKey);
    public string SignUpdateAdmins(UpdateAdminsMessage message, string privateKey);
    public Task<string> SignRegisterModuleAsync(RegisterModuleMessage message, IWeb3 web3);
    public Task<string> SignUnregisterModuleAsync(UnregisterModuleMessage message, IWeb3 web3);
    public Task<string> SignUpdateAdminsAsync(UpdateAdminsMessage message, IWeb3 web3);
    public byte[] GetMessageHash<T>(T message);
    public bool VerifySignature<T>(T message, string signature, string expectedAddress);
    public string RecoverSigner<T>(T message, string signature);
}
```

The EIP-712 domain is fixed to `("SmartAccountFactoryGovernance", "1", chainId, verifyingContract)`. `RegisterModuleMessage`, `UnregisterModuleMessage` and `UpdateAdminsMessage` are the three `[Struct(...)]`-attributed typed-data payloads it signs.

```csharp
public class FactoryGovernanceMultiSig
{
    public FactoryGovernanceMultiSig(BigInteger chainId, string factoryAddress);

    public string FactoryAddress { get; }
    public BigInteger ChainId { get; }

    public RegisterModuleProposal CreateRegisterModuleProposal(byte[] moduleId, string moduleAddress, BigInteger nonce, BigInteger deadline);
    public UnregisterModuleProposal CreateUnregisterModuleProposal(byte[] moduleId, BigInteger nonce, BigInteger deadline);
    public UpdateAdminsProposal CreateUpdateAdminsProposal(List<string> newAdmins, BigInteger newThreshold, BigInteger nonce, BigInteger deadline);
    public string SignRegisterModule(RegisterModuleProposal proposal, string privateKey);
    public string SignUnregisterModule(UnregisterModuleProposal proposal, string privateKey);
    public string SignUpdateAdmins(UpdateAdminsProposal proposal, string privateKey);
    public bool HasSufficientSignatures<T>(T proposal, int threshold) where T : IMultiSigProposal;
    public List<string> GetUniqueSigners<T>(T proposal) where T : IMultiSigProposal;
}
```

`FactoryGovernanceMultiSig` wraps a `FactoryGovernanceSigner` and adds the multi-sig bookkeeping: each `Create*Proposal` builds the typed-data message and its hash, `Sign*` appends a raw 65-byte signature to `proposal.Signatures`, and `GetUniqueSigners` recovers each signature's signer over the proposal's message hash (de-duplicating by address) so a caller can check `HasSufficientSignatures` against its own threshold.

## Validation

`Validation/` holds the local (off-chain) validation result shape and the ERC-4337 `validationData` packing rules — used by any bundler/mempool component that needs to evaluate a UserOperation's own validation outcome without a full EntryPoint simulation round-trip.

```csharp
public class UserOpValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public UserOpValidationError ErrorCode { get; set; } = UserOpValidationError.None;
    public BigInteger ValidationData { get; set; }
    public BigInteger PaymasterValidationData { get; set; }
    public ulong ValidAfter { get; set; }
    public ulong ValidUntil { get; set; }
    public string? Aggregator { get; set; }
    public BigInteger PreVerificationGas { get; set; }
    public BigInteger VerificationGasLimit { get; set; }
    public BigInteger CallGasLimit { get; set; }

    public static UserOpValidationResult Success();
    public static UserOpValidationResult Failure(string error, UserOpValidationError code = UserOpValidationError.Unknown);
}
```

```csharp
public static class ValidationDataCodec
{
    public const uint SIG_VALIDATION_FAILED = 1;

    public static BigInteger Pack(bool sigFailed, ulong validUntil, ulong validAfter, string? aggregator = null);
    public static (bool SigFailed, ulong ValidUntil, ulong ValidAfter, string? Aggregator) Parse(BigInteger validationData);
    public static bool IsValidNow(BigInteger validationData);
    public static BigInteger Merge(BigInteger accountValidation, BigInteger paymasterValidation);
}
```

`ValidationDataCodec` packs/unpacks the ERC-4337 `validationData` `uint256` (aggregator-or-sigFailed in the low 160 bits, `validUntil` in the next 48, `validAfter` in the 48 above that) and `Merge`s an account's and a paymaster's `validationData` into the single combined value the EntryPoint applies — `sigFailed` is OR'd, `validUntil` takes the tighter (non-zero, minimum) bound, and `validAfter` takes the looser (maximum) bound.

```csharp
public static class Erc7769ErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidFields = -32602;
    public const int InternalError = -32603;
    public const int SimulateValidation = -32500;
    public const int SimulatePaymasterValidation = -32501;
    public const int OpcodeValidation = -32502;
    public const int NotInTimeRange = -32503;
    public const int Reputation = -32504;
    public const int InsufficientStake = -32505;
    public const int UnsupportedSignatureAggregator = -32506;
    public const int InvalidSignature = -32507;
    public const int PaymasterDepositTooLow = -32508;
    public const int UserOperationReverted = -32521;
}
```

## EntryPoint Simulations

`EntryPointSimulations/` defines the ABI for `EntryPointSimulations.sol`'s `simulateValidation`/`simulateHandleOp` — the eth-infinitism helper contract a bundler calls (via `eth_call` against a state override) to run full ERC-4337 validation without submitting a real transaction.

```csharp
[Function("simulateValidation", typeof(SimulateValidationOutputDTO))]
public class SimulateValidationFunction : FunctionMessage
{
    [Parameter("tuple", "userOp", 1)]
    public virtual PackedUserOperation UserOp { get; set; }
}

public class ValidationResult
{
    public virtual ReturnInfo ReturnInfo { get; set; }
    public virtual StakeInfo SenderInfo { get; set; }
    public virtual StakeInfo FactoryInfo { get; set; }
    public virtual StakeInfo PaymasterInfo { get; set; }
    public virtual AggregatorStakeInfo AggregatorInfo { get; set; }
}

public class ReturnInfo
{
    public virtual BigInteger PreOpGas { get; set; }
    public virtual BigInteger Prefund { get; set; }
    public virtual BigInteger AccountValidationData { get; set; }
    public virtual BigInteger PaymasterValidationData { get; set; }
    public virtual byte[] PaymasterContext { get; set; }
}

public class StakeInfo
{
    public virtual BigInteger Stake { get; set; }
    public virtual BigInteger UnstakeDelaySec { get; set; }
}
```

`SimulateValidationFunction` (and the companion `SimulateHandleOpFunction`, which additionally executes the op against a `target`/`targetCallData`) are `FunctionMessage`s decoded into `ValidationResult` (respectively `ExecutionResult`) — `ValidationResult.SenderInfo`/`FactoryInfo`/`PaymasterInfo` are each a `StakeInfo` reporting that entity's on-chain stake and unstake delay, and `ReturnInfo` carries the packed `accountValidationData`/`paymasterValidationData` this package's own `ValidationDataCodec` (above) parses.

## Architecture Overview

```
Your Application
       │
       ▼
┌─────────────────────────┐
│   IAAClient              │  (CreateAccountAsync / GetAccount / Configure)
└───────────┬─────────────┘
            │ Configures
            ▼
┌─────────────────────────┐
│   Contract Service      │  (ERC20ContractService, your generated services, etc.)
│   with AAContractHandler│
└───────────┬─────────────┘
            │ Creates UserOperation
            ▼
┌─────────────────────────┐
│   Bundler Service       │  (IAccountAbstractionBundlerService)
│   eth_sendUserOperation │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Bundler               │  (Collects UserOps, creates bundle)
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   EntryPoint Contract   │  (handleOps)
│   0x4337090...eD8D009   │  (v0.9)
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Your Smart Account    │  (NethereumAccount, SimpleAccount, etc.)
│   execute(target, data) │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Target Contract       │  (ERC20, your contract, etc.)
└─────────────────────────┘
```

## Further Reading

- [ERC-4337 Specification](https://eips.ethereum.org/EIPS/eip-4337)
- [Nethereum Documentation](https://docs.nethereum.com/)
- [Account Abstraction Resources](https://www.erc4337.io/)
