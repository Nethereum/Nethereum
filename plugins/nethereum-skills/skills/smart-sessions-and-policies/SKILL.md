---
name: smart-sessions-and-policies
description: "Help users grant a limited, revocable session key to a bot, backend service, or AI agent instead of handing over full control of an ERC-4337 smart account, using Nethereum's SmartSession validator and policies (SudoPolicy, UniActionPolicy, ERC20SpendingLimitPolicy). Use whenever the user mentions session keys, scoped permissions, delegated signing for a bot/agent, spending limits on a smart account, revocable wallet access, automated trading bot authorization, or SmartSession/permissionId, in .NET/C#."
user-invocable: true
---

# Smart Sessions and Policies: Scoped, Revocable Session Keys

An automated trading bot, a backend service that pays out rewards, an AI agent acting on a user's behalf — all of them need to sign UserOperations, but none of them should hold the account owner's key. Handing over the owner key gives unlimited, permanent authority: it can call anything, spend anything, forever, until someone rotates the key everywhere. A **session key** is the alternative: a fresh, disposable keypair that authenticates through the account's `SmartSession` [ERC-7579](../modular-accounts/SKILL.md) validator, constrained by one or more **policies** that say exactly what it may do — call this one function, transfer up to this many tokens — and nothing else. Revoke it and the authority is gone; the owner key never left cold storage.

## When to Use This

- User wants to **let a bot/agent/backend act on an account without giving it full control**
- User wants a **spending limit** or **one-function-only** permission for a delegated key
- User mentions **session keys, scoped permissions, or revocable access** for a smart account
- User is building **automation**: recurring payments, DCA bots, reward payouts, agentic execution
- User wants to know **what happens when a call is out of policy**

## Packages

```bash
dotnet add package Nethereum.Web3
dotnet add package Nethereum.AccountAbstraction
```

You need an `IAAClient` and an already-deployed, ERC-7579-compatible smart account with at least one validator (see the `modular-accounts` skill). On top of that, sessions deploy generated Nethereum contract services — none requiring hand-written ABI:

| Contract | Generated service | Role |
|---|---|---|
| `SmartSession` | `SmartSessionService` | The `TYPE_VALIDATOR` module every session authenticates through |
| `ECDSASessionValidator` | `ECDSASessionValidatorService` | The `ISessionValidator` a session's signature is checked against — separate from the account's own `ECDSAValidator` |
| `SudoPolicy` | `SudoPolicyService` | An action/UserOp policy that always allows — "no further restriction" |
| `UniActionPolicy` | `UniActionPolicyService` | An action policy with per-argument conditions and a value limit |
| `ERC20SpendingLimitPolicy` | `ERC20SpendingLimitPolicyService` | An action policy that tracks cumulative ERC-20 spend against a limit |

```csharp
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy;
```

## The Simple Way

```csharp
using Nethereum.AccountAbstraction; // AATransactionReceipt
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Signer;

// 1. A fresh keypair - the agent/bot process holds ONLY this, never the account owner's key.
var sessionKey = EthECKey.GenerateKey();

// 2. Scope it to exactly one action: call count() on `counterAddress`, no other function, no other
//    target. SudoPolicy means "no further restriction on the args of THIS action" - see below for
//    when you'd use UniActionPolicy or a spending limit instead.
var salt = new byte[32];
Guid.NewGuid().ToByteArray().CopyTo(salt, 0);

var config = new SmartSessionConfig()
    .WithSessionValidator(sessionValidatorAddress)              // a deployed ECDSASessionValidator
    .WithSessionValidatorInitData(sessionKey.GetPublicAddress())
    .WithSalt(salt)
    // UnrestrictedAction's selector arg is the 4-byte function selector as a hex string, e.g. "0x06661abd".
    .WithAction(ActionDataBuilder.UnrestrictedAction(counterAddress, countSelector, sudoPolicyAddress));

// 3. Enable it: installs SmartSession (first time only) and this session, in one UserOp, signed by
//    the account's OWNER key - the session key never touches this step.
config.ModuleAddress = smartSessionAddress;
var accountService = new NethereumAccountService(web3, account.Address);
accountService.UseAccountAbstraction(account, aaClient);
await accountService.InstallModuleAndWaitForReceiptAsync(config);

// 4. Attach a SEPARATE NethereumSmartAccount for the SAME address, authenticated by the session key.
var permissionId = await smartSession.GetPermissionIdQueryAsync(config.ToSession());
var sessionAccount = aaClient.GetAccount(
    account.Address,
    new SmartSessionKeySigningService(sessionKey, permissionId),
    new SmartSessionValidatorModule(smartSessionAddress, permissionId));

// 5. From here, the session key signs - the owner key is never involved again.
counter.UseAccountAbstraction(sessionAccount, aaClient);
var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
```

## Mental Model: A Session Is a Scoped Permission Set

`SmartSession` is one ERC-7579 validator contract shared by every session on every account. A session's identity is its `PermissionId` — a `bytes32` derived **only** from `SessionValidator`, `SessionValidatorInitData`, and `Salt` (`SmartSessionConfig.ToSession()`). Two sessions with the same validator, init data, and salt collide on the same `PermissionId` even if their actions differ — always use a fresh, random salt per session.

Policies attach at two scopes:

- **`UserOpPolicies`** (`WithUserOpPolicy`/`WithSudoPolicy`) — evaluated once per UserOperation, regardless of which function it calls. Use for session-wide gates.
- **`Actions`** (`WithAction`/`ActionDataBuilder`) — evaluated per `(target, selector)` pair. This is where most real scoping happens.

| Policy contract | What it allows | Nethereum builder |
|---|---|---|
| `SudoPolicy` | Everything, unconditionally, for the scope it's attached to | `WithSudoPolicy(address)` / `ActionDataBuilder.WithSudoPolicy` |
| `UniActionPolicy` | One function, with per-argument conditions and/or a native-value limit | `UniActionPolicyBuilder` |
| `ERC20SpendingLimitPolicy` | `transfer`/`approve`/`transferFrom` up to a cumulative token limit | `ERC20SpendingLimitBuilder` |

A single session can mix a `SudoPolicy`-scoped action with a `UniActionPolicy`-scoped one.

## SudoPolicy: Unconstrained Within the Scope

`SudoPolicy` scoped to a **specific action** is safe: the session can still only call that one `(target, selector)` — it just means "don't restrict the arguments further." Reach for it when a function has nothing worth restricting, e.g. a zero-argument function like `count()`:

```csharp
var result = await sudoPolicyService.CheckActionQueryAsync(
    configId, account, target, value, calldata);
// result == 0 (SIG_VALIDATION_SUCCESS) - SudoPolicy never inspects arguments.
```

`SudoPolicy` scoped as a **`UserOpPolicy`** is a much bigger grant — it removes UserOp-level gating entirely, leaving only whatever action policies you've attached (or none) as the real boundary.

## UniActionPolicy: One Action, With Rules

`UniActionPolicy` is the tool for "this session may call this one function, and here's what its arguments must look like":

```csharp
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;

// A DCA bot may call swap() on ONE router, up to 100 (token-unit) worth of value per call.
var maxSwapValue = BigInteger.Parse("100000000");

var policyData = new UniActionPolicyBuilder()
    .WithValueLimit(maxSwapValue)
    .Build();

var session = new SmartSessionConfig()
    .WithSessionValidator(sessionKeyValidator)
    .WithSalt(salt)
    .WithAction(new ActionDataBuilder()
        .WithTarget(uniswapRouter)
        .WithSelector(swapSelector)
        .WithUniActionPolicy(uniActionPolicy, policyData)
        .Build())
    .ToSession();
```

`WithValueLimit` caps the native-token `value` sent with the call unconditionally. For calldata arguments, add up to 16 `ParamRule`s, each comparing one 32-byte word at a fixed offset into the call's **argument data** (not raw calldata — argument 0 starts right after the 4-byte selector):

```csharp
var policyInitData = new UniActionPolicyBuilder()
    .WithEqualityCheck(offset: 0, expectedValue: ownerAddress.HexToByteArray())  // arg 0 must equal owner
    .WithMaxValue(offset: 32, maxValue: capBytes)                                // arg 1 <= cap
    .WithLimitedUsage(offset: 32, refValue: capBytes, limit: totalBudget)        // AND track cumulative usage
    .Build();
```

**A zero-argument function cannot be scoped by `UniActionPolicy`** — its `checkAction` requires at least one `ParamRule` and unconditionally reads 32 bytes of argument data, so it reverts on a truly zero-argument call. Use `SudoPolicy` for that action instead.

## Session Lifecycle: Enable → Use → Out-of-Policy Rejection

### Enable

Installs `SmartSession` (first time) and the session's policies together, signed by the account's **existing** validator — the session key isn't involved yet:

```csharp
var sessionConfig = new SmartSessionConfig()
    .WithSessionValidator(config.SessionValidatorAddress)
    .WithSessionValidatorInitData(sessionKey.GetPublicAddress())
    .WithSalt(salt)
    .WithAction(new ActionDataBuilder()
        .WithTarget(counterAddress).WithSelector(countSelector).WithSudoPolicy(config.SudoPolicyAddress).Build())
    .WithAction(new ActionDataBuilder()
        .WithTarget(counterAddress).WithSelector(gasWasterSelector)
        .WithUniActionPolicy(config.UniActionPolicyAddress, gasWasterCapInitData).Build());

var session = sessionConfig.ToSession();
var permissionId = await smartSession.GetPermissionIdQueryAsync(session);

if (!smartSessionInstalled)
{
    // First session on this account: install SmartSession AND enable this session in the SAME UserOp.
    sessionConfig.ModuleAddress = config.SmartSessionAddress;
    receipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(sessionConfig);
}
else
{
    // SmartSession already installed (a prior session exists): a further session goes through
    // SmartSession's own enableSessions, itself a self-call.
    smartSession.UseAccountAbstraction(account, client);
    receipt = (AATransactionReceipt)await smartSession.EnableSessionsRequestAndWaitForReceiptAsync(
        new List<Session> { session });
}
```

### Use

`IAAClient.GetAccount` attaches a **second, independent** `NethereumSmartAccount` to the same address, authenticated by the session key:

```csharp
var sessionAccount = client.GetAccount(
    account.Address,
    new SmartSessionKeySigningService(sessionKey, permissionId),
    new SmartSessionValidatorModule(config.SmartSessionAddress, permissionId));

counter.UseAccountAbstraction(sessionAccount, client);
var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync();
```

`SmartSessionKeySigningService.SignTypedDataV4` signs the UserOperation's EIP-712 digest with the raw session key, then wraps it in SmartSession's USE-mode envelope (`mode(1 byte) || permissionId(32 bytes) || signature(65 bytes)`). **`PersonalSign` throws `NotSupportedException`** — only `SignTypedDataV4` (UserOperation signing) is supported.

### Out-of-Policy Rejection

A call the session key *cannot* make fails on-chain during validation, before it does anything:

```csharp
// Within the UniActionPolicy cap of 3 - succeeds.
var allowedReceipt = (AATransactionReceipt)await counter.GasWasterRequestAndWaitForReceiptAsync(repeat: 1, "");
// allowedReceipt.UserOpSuccess == true

// Beyond the cap - the session key CANNOT do this, and the call is rejected.
try
{
    var overCapReceipt = (AATransactionReceipt)await counter.GasWasterRequestAndWaitForReceiptAsync(repeat: 100, "");
}
catch (Exception ex)
{
    // gasWaster(100) was rejected: UniActionPolicy's checkAction failed the argument rule.
}
```

`SmartSession.validateUserOp` treats a failed policy result as fatal — it reverts with `PolicyViolation(permissionId, policy)` during UserOperation **validation**, so the call never executes: no partial state change, no gas spent on the action itself. `ERC20SpendingLimitPolicy` fails the identical way the moment `alreadySpent + amount > spendingLimit`.

### Revoke

```csharp
smartSession.UseAccountAbstraction(account, aaClient);
await smartSession.RemoveSessionRequestAndWaitForReceiptAsync(permissionId);
```

`SmartSession.removeSession(permissionId)` is a self-call, like `enableSessions`, and clears every policy and the session validator entry for that `permissionId`. **This on-chain call is the only thing that actually revokes authority** — any local `ValidAfter`/`ValidUntil` bookkeeping is application-side only and does not touch the chain; a session key your app has "forgotten" but never revoked on-chain can still sign valid UserOperations for as long as its policies allow.

## Common Mistakes

- **Reusing a salt across sessions with different actions** — they collide on the same `PermissionId`, and a later install/`enableSessions` overwrites the earlier session's action set.
- **Scoping a zero-argument function with `UniActionPolicy`** — reverts. Use `SudoPolicy` instead.
- **Calling `PersonalSign`** on a session-key account — throws `NotSupportedException`.
- **Forgetting `.WithPaymasterPermission(true)`** — if a paymaster sponsors this session's gas, the session itself must permit it, or the bundler rejects the UserOperation.
- **Assuming revocation is local** — only the on-chain `removeSession` call stops a session key from signing.

## Decision Guide

| | Session key (`SmartSession`) | Second validator (e.g. passkey) | Social recovery |
|---|---|---|---|
| Who holds the key | An automated process, bot, or agent | A person (you, a co-signer) | A set of guardians |
| What it can do | Exactly what its policies allow | Anything the account allows that validator to do | Nothing day-to-day; only triggers recovery |
| Revocation | `SmartSession.removeSession(permissionId)` — instant, targeted | Uninstall that validator module | N/A |
| Use it for | Automation: bots, backends, recurring/scheduled operations | A second full-control device or passkey | Recovering access after losing every owner key |

Reach for a session key whenever the caller shouldn't have full account control even temporarily. For recovering a lost owner key entirely, see the `social-recovery` skill.

For full documentation, see: https://docs.nethereum.com/docs/account-abstraction/guide-smart-sessions-and-policies
