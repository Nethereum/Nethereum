---
name: social-recovery
description: "Help users recover an ERC-4337 smart account after the owner key is lost, using an N-of-M guardian quorum — no single guardian can act alone. Covers Nethereum's SocialRecoveryConfig, SocialRecoveryValidatorModule, and MultiGuardianSigningService. Use whenever the user mentions social recovery, guardian recovery, N-of-M guardians, 'lost my seed phrase / lost my wallet key', recovering a smart contract wallet, break-glass account recovery, or guardian quorum wallet recovery, in .NET/C#."
user-invocable: true
---

# Social Recovery: N-of-M Guardian Quorum Account Recovery

Every other signer (an ECDSA key, a passkey, a session key) assumes there is *some* working credential to authenticate with. Social recovery answers the question those cannot: **what happens when the owner key itself is gone?** A phone is lost, a seed phrase is destroyed, a hardware wallet dies — no key remains to sign anything, so no session key, second validator, or policy can help, because all of them still need to be invoked by an existing authorized signer.

Social recovery must be set up **before** that happens: a set of guardians (friends, family, hardware wallets you control, other trusted parties) is installed on the account in advance, so that later, if the owner key is lost, a **quorum** of guardians — not any single one — can co-sign a UserOperation that rotates the account to a new owner key. No individual guardian can act alone, and guardians have no day-to-day authority over the account — only the collective quorum, exercised once, to recover it.

## When to Use This

- User wants a way to **recover a smart account after losing the owner key**
- User mentions **guardians, N-of-M quorum, or break-glass recovery** for a wallet
- User is designing an account-recovery UX that shouldn't depend on any single device or person surviving
- User wants to know **who can act during recovery** and what happens with too few guardians

## Packages

```bash
dotnet add package Nethereum.AccountAbstraction
dotnet add package Nethereum.Web3
```

You need an `IAAClient`, a deployed ERC-7579-compatible `NethereumAccount` with at least one validator installed (typically `ECDSAValidator` — see the `modular-accounts` skill), plus a deployed `SocialRecovery` module contract (`SocialRecoveryService.DeployContractAndGetServiceAsync`, a generated Nethereum contract service — no hand-written ABI).

```csharp
using System.Linq;
using Nethereum.AccountAbstraction; // AATransactionReceipt
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Validation; // Erc7769ErrorCodes
using Nethereum.JsonRpc.Client; // RpcResponseException
using Nethereum.Signer; // EthECKey
```

## The Simple Way

```csharp
var accountService = new NethereumAccountService(web3, account.Address);
accountService.UseAccountAbstraction(account, aaClient);

// Install SocialRecovery with a 2-of-3 guardian quorum, signed by the account's CURRENT owner.
var receipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
    socialRecoveryAddress, threshold: 2, new[] { guardian1, guardian2, guardian3 });
```

That's it — any 2 of those 3 guardians can now recover the account if its owner key is ever lost. ABI-encoding the guardian list, the module type id, fees, gas, and nonce are all automatic.

## Mental Model: Guardians, Threshold, Recovery-as-Owner-Rotation

`SocialRecovery` is an ERC-7579 `TYPE_VALIDATOR` module, installed exactly like `ECDSAValidator` or `SmartSession`, with two pieces of on-chain configuration per account: a **guardian list** and a **threshold** (`SocialRecoveryConfig`). Guardians never get day-to-day control. The only thing a guardian quorum can do is co-sign a UserOperation, and that UserOperation is itself restricted on-chain to a single `execute()` call whose target is a currently-installed `TYPE_VALIDATOR` module — in practice, calling `transferOwnership` on the account's `ECDSAValidator` to rotate the owner to a new key.

Recovery is a **single co-signed UserOperation**, not a two-phase propose/approve flow: each guardian signs the same userOp hash with their own key, `MultiGuardianSignatureBlobBuilder` concatenates the `threshold` signatures (ascending by guardian address) into one `CheckNSignatures`-compatible blob, and `SocialRecoveryValidatorModule` prefixes that blob with the module's own address.

| Type | Role |
|---|---|
| `SocialRecoveryConfig` | Builds the module's install-time init data — `Threshold` + `Guardians` |
| `SocialRecoveryValidatorModule` | The `IErc7579ValidatorModule` that identifies the installed module and prefixes a recovery signature |
| `MultiGuardianSigningService` | The `IAccountSigningService` that has a quorum of guardian keys co-sign a UserOperation |

## Configure an N-of-M Guardian Quorum

Guardians must be **sorted ascending by address and free of duplicates** at install time — `SocialRecovery.sol`'s `onInstall` reverts `NotSortedAndUnique()` otherwise. `MultiGuardianSignatureBlobBuilder.SortAddressesAscending` gives you that ordering:

```csharp
var guardianKeys = Enumerable.Range(0, guardianCount)
    .Select(_ => EthECKey.GenerateKey())
    .ToArray();

var sortedGuardians = MultiGuardianSignatureBlobBuilder.SortAddressesAscending(
    guardianKeys.Select(k => k.GetPublicAddress()));

var accountService = new NethereumAccountService(web3, account.Address);
accountService.UseAccountAbstraction(account, aaClient);

var receipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
    socialRecoveryAddress, threshold, sortedGuardians);
```

This is signed by the account's **current owner** — the guardians are not involved in their own installation. It composes with counterfactual deploy-on-first-op exactly like any other first UserOperation: if the account isn't deployed yet, the same op both deploys it and installs `SocialRecovery`.

`SocialRecoveryConfig` also validates itself before ever reaching the chain: `GetInitData()` throws `InvalidOperationException` if `Guardians` is empty, or if `Threshold` is not between 1 and the guardian count — the same rule `SocialRecovery.sol`'s own `onInstall` enforces on-chain.

## A Guardian Quorum Recovers the Account

The owner key is now lost. A quorum of `threshold` guardians (any `threshold` of the installed set — order doesn't matter, only which ones sign) co-signs a UserOperation that calls `transferOwnership` on the account's `ECDSAValidator`, through a **second, independent** `NethereumSmartAccount` attached to the same address:

```csharp
var newOwner = EthECKey.GenerateKey();

// A guardian quorum co-signs the recovery userOp - the account's owner is never involved.
var guardianSigningService = new MultiGuardianSigningService(guardianKeys, threshold);
var socialRecoveryValidator = new SocialRecoveryValidatorModule(socialRecoveryAddress, threshold);
var recoveryAccount = aaClient.GetAccount(account.Address, guardianSigningService, socialRecoveryValidator);

// Driven through the RECOVERY TARGET's own typed service (ECDSAValidatorService), not
// NethereumAccountService.ExecuteAsync - see Common Mistakes below for why that distinction matters.
var ecdsaValidatorService = new ECDSAValidatorService(web3, ecdsaValidatorAddress);
ecdsaValidatorService.UseAccountAbstraction(recoveryAccount, aaClient);

var receipt = (AATransactionReceipt)await ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(
    newOwner.GetPublicAddress());

var rotatedOwner = await ecdsaValidatorService.GetOwnerQueryAsync(account.Address);
// rotatedOwner == newOwner.GetPublicAddress() - the owner has genuinely rotated.
```

Neither the old owner key nor the new owner key ever signs anything — the rotation is authorized entirely by the guardian quorum through `SocialRecoveryValidatorModule`.

## Under-Threshold Rejection Is Enforced On-Chain

Fewer guardians than the threshold genuinely cannot recover the account — the chain rejects it, not just client-side discouragement:

```csharp
var partialGuardianKeys = guardianKeys.Take(threshold - 1).ToArray();
var partialThreshold = partialGuardianKeys.Length; // one short of the installed threshold

var partialSigningService = new MultiGuardianSigningService(partialGuardianKeys, partialThreshold);
var partialValidator = new SocialRecoveryValidatorModule(socialRecoveryAddress, partialThreshold);
var partialAccount = aaClient.GetAccount(account.Address, partialSigningService, partialValidator);

var attemptedNewOwner = EthECKey.GenerateKey();
ecdsaValidatorService.UseAccountAbstraction(partialAccount, aaClient);

try
{
    await ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(attemptedNewOwner.GetPublicAddress());
}
catch (InvalidOperationException ex) when (ex.InnerException is RpcResponseException rpcEx &&
                                             rpcEx.RpcError.Code == Erc7769ErrorCodes.SimulateValidation)
{
    // Rejected during bundler gas estimation (AA23) - the undersized signature blob fails
    // CheckSignatures' length guard.
}
```

The rejection happens during the bundler's gas-estimation simulation, before the real op is ever sent. A distinct failure mode exists too: a correctly-**sized** blob signed entirely by non-guardian keys passes the length guard but fails `SocialRecovery`'s guardian-membership check instead, surfacing as `AA24` at send time rather than `AA23` during estimation.

## Advanced: Changing the Guardian Set Later

`SocialRecoveryService` also exposes `AddGuardianRequestAsync(guardian)`, `RemoveGuardianRequestAsync(prevGuardian, guardian)`, and `SetThresholdRequestAsync(threshold)`. On-chain these are self-calls (`msg.sender == account`) — driven through whichever validator currently authorizes the account (typically the owner's `ECDSAValidator`), not the recovery quorum. Use them to evolve a guardian set while the existing owner key still works, not as part of a lost-key recovery.

## Common Mistakes

- **Guardians not sorted/deduplicated before install** — `SocialRecovery.sol` reverts `NotSortedAndUnique()`. Always run addresses through `MultiGuardianSignatureBlobBuilder.SortAddressesAscending` first.
- **Routing the recovery call through `NethereumAccountService.ExecuteAsync` instead of the target module's own service.** `SocialRecovery.validateUserOp`'s on-chain decode requires a single `execute()` call targeting a currently-installed `TYPE_VALIDATOR` module. Routing through the account's own `ExecuteAsync` wraps the call in a *second* `execute()` — a shape `SocialRecovery`'s fixed-offset decode doesn't recognize. Always attach the target module's own generated service (e.g. `ECDSAValidatorService`) via `UseAccountAbstraction`.
- **Threshold out of range** — `SocialRecoveryConfig.GetInitData()` throws client-side for an empty guardian list or a threshold of zero or greater than the guardian count.
- **Assuming under-threshold and non-guardian quorums fail the same way** — too few signatures fails the length guard during estimation (`AA23`); enough signatures from the wrong keys fails the membership check at send time (`AA24`). Catch both.

## Decision Guide

| | Social recovery | Second validator (e.g. passkey) | Session key (`SmartSession`) |
|---|---|---|---|
| When it's usable | Only after the primary owner key is lost | Any time | Any time, within its policy scope |
| Who can act alone | Nobody — a quorum is required | Yes | Yes, within whatever the policy allows |
| Needed in advance | Guardians + threshold, installed before the key is lost | The second credential, already installed | A session already enabled |
| Solves "I lost my only key" | Yes — this is exactly what it's for | Only if you still have the *other* credential | No — a session key can't rotate the owner |

Reach for social recovery when the account needs a way back in that doesn't depend on any single device or person surviving. It's complementary to a second validator (fast, no quorum) and a session key (scoped delegated access, covered by the `smart-sessions-and-policies` skill).

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-social-recovery
