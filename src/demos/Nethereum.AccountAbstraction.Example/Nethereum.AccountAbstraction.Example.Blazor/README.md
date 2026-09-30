# Nethereum Account Abstraction Example - Blazor Server head

A runnable Blazor Server front end for the AA example's thirteen tabs: **Setup infra** (deploy the
AA stack on demand), **Setup**, **Send**, **Batch**, **Gasless**, **Custom Contract**, **Passkey**
(WebAuthn), **EIP-7702** (upgrade an EOA in place), **Modules** (ERC-7579 install/uninstall),
**Policies** (SmartSession session keys scoped by policy), **Workflows** (ERC-7579
`OwnableExecutor` - a delegated automation agent acts on your account without your account signing
each operation), **Social Recovery** (ERC-7579 `SocialRecovery` - an N-of-M guardian quorum rotates
the account's owner after a simulated "lost key", with an under-threshold quorum rejected
on-chain), and **Diagnostics** (a developer/observability view of a UserOperation - the same
`count()`/`countFail()` calls other tabs use, run to a genuine success, a genuine on-chain revert,
and a genuine out-of-gas failure, with the real receipt, gas, bundler lookup and packed-wire data
behind each). Every component only binds to a view model from
`Nethereum.AccountAbstraction.Example.Core` (`@inject` + `RelayCommand`s) - there is no AA logic or
raw ABI handling in this project.

## Running it

```
dotnet run --project src/demos/Nethereum.AccountAbstraction.Example/Nethereum.AccountAbstraction.Example.Blazor
```

`dotnet run` needs no external node, bundler, or configuration - and deploys nothing until you ask
it to: the app builds and starts serving immediately, landing on **0. Setup infra**. Nethereum ships
a full ERC-4337 bundler (`Nethereum.AccountAbstraction.Bundler`), not just a client, and tab0 offers
three modes for running it. **Embedded** (default) brings up an in-process DevChain plus an
in-process bundler with ERC-4337 validation ON (`Nethereum.AccountAbstraction.Bundler.InProcess`),
then deploys the EntryPoint/ECDSAValidator/NethereumAccountFactory stack, the demo
`TestCounter`/`BookingRegistry` contracts, and a funded sponsoring `VerifyingPaymaster` - see
`Nethereum.AccountAbstraction.Example.Hosting`'s `HostBootstrap.StartInfraAsync`/`DeployStackAsync`,
which the test suite's `ExampleFixture` uses the same way, and which is shared (via the injected
`IEmbeddedInfrastructureProvisioner`) with the Avalonia head. **External** deploys the SAME stack
against a node + bundler you already run, given their RPC urls and a funder private key - the SAME
client + bundler-API, just pointed elsewhere (`IExternalInfrastructureProvisioner`); the bundler's
own EntryPoint is reused if it is already on-chain, otherwise a fallback is deployed through the
funder. **Use existing** points that same client + bundler-API at a PROVIDER's already-deployed
standard ERC-7579 modules instead (`IExistingInfrastructureProvisioner`) - client-side interop
testing - and deploys only this demo's own target contracts through the funder; there is no
paymaster in this mode, so **Gasless** is unavailable. Switching between test-local and real
infrastructure is just a different endpoint (plus, for Use existing, the provider's published
module addresses). Every other tab is disabled with a "deploy first" message until deployment
finishes (typically a few seconds) - watch tab0's own log for progress.

Every tab renders under a shared header banner (`AccountBannerViewModel`, injected by
`MainLayout.razor`) showing whichever account is currently active: its address, who/what controls it
(an ECDSA owner key, a passkey, ...), whether it has deployed, and its DevChain balance - it updates
itself whenever any tab creates an account or funds one, so it always reflects the account the tabs
below are acting on.

## Click-through order

0. **Setup infra** - click *Deploy*. Nothing else works until this finishes; see "Running it" above.
1. **Setup** - pick a payment mode (**Self-funded**, the default, or **Paymaster-sponsored** if a
   paymaster is configured), then click *Create Account*. This only derives a counterfactual
   address; no transaction is sent yet. For a self-funded account, click *Fund account* - it starts
   at zero balance on a real network, and **Send**/**Batch**/**Custom Contract** all pay their own
   gas, so they need funds first; a paymaster-sponsored account skips this, since every action tab
   applies the sponsorship automatically. (This funding button is a DevChain-only faucet, not an AA
   concept - see `HostBootstrap.FundAsync`. `SetupViewModel.FundAccountCommand` funds whichever
   account is currently active - through the shared `IDevChainFaucet` seam - so it works for a
   passkey account created on the **Passkey** tab too, not just one created here.)
2. **Send** - *Send Count* deploys the account on its first operation and increments the on-chain
   counter; *Send Failing Count* always reverts, showing how a rejected UserOperation surfaces as an
   error message.
3. **Batch** - two calls as one UserOperation; the failing variant shows the whole batch reverting
   atomically.
4. **Gasless** - works even without funding the account (a paymaster sponsors the gas). Shows the
   account's ETH balance before/after to prove it never paid. If you already funded the account in
   step 1, the balance will be unchanged-but-nonzero rather than unchanged-at-zero - either way,
   "unchanged" is the proof the paymaster paid.
5. **Custom Contract** - book a slot, query who holds it, and try releasing it (it always reverts,
   because the session's account is never the registry's owner). Booking the same slot twice from
   two different app runs would surface the typed `SlotAlreadyBooked` custom error.
6. **Passkey** - pick a payment mode (same choice as Setup), then click *Create Passkey Account*,
   which prompts your browser for a real platform authenticator and creates a smart account owned by
   that P-256 passkey through the same generic on-ramp every other signer uses. *Fund account* tops
   it up (skip it if paymaster-sponsored), then *Send Count (passkey-signed)* drives the counter
   through a UserOperation signed by the passkey instead of an ECDSA key.
7. **EIP-7702** - *Generate EOA* creates a plain wallet (no smart-account code yet, unlike every
   other tab's counterfactual CREATE2 account). *Fund EOA* tops it up, then *Upgrade via EIP-7702 &
   Send* sends ONE UserOperation from that EOA that both delegates its code, via a signed EIP-7702
   authorisation, to the deployed `NethereumAccount` implementation and installs the ECDSA
   validator, then executes a counter call - same address, now a smart account.
8. **Modules** - on the session's active account (still owned by its original ECDSA key), *Install
   passkey as second validator* installs the shared `WebAuthnValidator` as a SECOND validator (a
   self-call UserOperation). *Prove passkey can sign (Count via passkey)* drives the counter
   through it to show the new validator genuinely authorises operations, not merely that
   `isModuleInstalled` returns true. *Uninstall passkey validator* removes it again.
9. **Policies** - *Create scoped session key* installs SmartSession (first time only) and enables
   one session with two actions: `count()` unconditionally allowed via a `SudoPolicy`, and
   `gasWaster(repeat)` capped via a `UniActionPolicy`. *Use session key (Count via session key)*
   proves the scoped key works without the account's owner key ever signing. *Try out-of-policy
   action (gasWaster over the cap)* calls `gasWaster` once within the cap (succeeds) and once
   beyond it, which SmartSession's own policy check rejects.
10. **Workflows** - *Create automation agent* generates and funds a separate EOA. *Grant the agent
    executor rights* installs the shared `OwnableExecutor` naming that agent as its owner. *Run
    delegated action (agent calls count())* has the agent's own plain transaction - no
    UserOperation, no owner signature - call `count()` on the account directly. *Try unauthorized
    action (different EOA)* makes the identical call from an unrelated EOA and shows it rejected by
    the executor's own owner check. *Revoke executor* removes it again.
11. **Social Recovery** - *Set up guardians* installs the shared `SocialRecovery` module with a fresh
    2-of-3 guardian set (a self-call UserOp signed by the account's current owner). *Lose key &
    recover with guardian quorum* simulates losing that owner key: a quorum of the guardians
    co-signs a recovery userOp - the account's actual owner key is never involved - that rotates the
    account's `ECDSAValidator` owner to a brand new key, driven through the target validator's own
    `transferOwnership` rather than the account's `execute()`. *Switch active account to new owner &
    prove it* rebuilds the session's active account under that new key and publishes it, then sends
    a `count()` UserOperation signed by it to prove the new key genuinely controls the account, not
    merely that `GetOwner` returns it. *Try under-threshold recovery (should be rejected)* attempts
    the same recovery with one fewer guardian than the installed threshold and shows the bundler
    rejecting it during gas estimation (AA23) - a sub-quorum cannot recover the account no matter how
    the signature blob is built, and the owner is left unchanged.
12. **Diagnostics** - a developer/observability view of a UserOperation on the session's active
    account. *Run diagnosed op (success)* sends a normal `count()` and populates every panel from
    REAL post-op data: the full `AATransactionReceipt` (not just `UserOpHash`/`UserOpSuccess`), the
    ACTUAL sent op from the bundler's own `eth_getUserOperationByHash` record, requested-vs-actually-
    charged gas (plus the `AAGasConfig` buffers/multiplier that shaped it), and the packed
    `accountGasLimits`/`gasFees` wire words re-derived via `PackedUserOperationGasExtensions`. *Run
    reverting op* sends `countFail()` through to a genuine on-chain revert (`RevertedWithReason`,
    with `RevertReason`/`FailureDiagnostic` populated) - the real bundler's own gas estimator would
    otherwise reject a guaranteed-revert call before it is ever sent, so this flavor bypasses only the
    estimate step to reach real mining, same as `Nethereum.AccountAbstraction.IntegrationTests`'
    `OutOfGasDiagnosticTests`. *Run out-of-gas op* sends the SAME `count()` with callGasLimit forced to
    1, producing a genuine `FailedWithoutReason`/`IsLikelyOutOfGas` receipt. The revert/out-of-gas
    flavors need the account already deployed - run the diagnosed op first.

All thirteen tab components stay mounted for the life of the page (hidden with CSS, not `@if`) so
switching tabs never loses a view model's state - `AddExampleCore` registers most of them transient
(tab0 and the account banner are singletons, like `SessionState` itself), and a
removed-then-recreated component would get a fresh, empty view model.

## Project shape

| File | Role |
|---|---|
| `Program.cs` | Builds and runs the `WebApplication` immediately - no deploy at startup. Calls `AddExampleHostDeferred()` (from `Nethereum.AccountAbstraction.Example.Hosting`) to register an empty `SessionState`, the embedded infra provisioner tab0 drives on demand, and `AddExampleCore()`. |
| `Nethereum.AccountAbstraction.Example.Hosting/HostBootstrap.cs` | `StartInfraAsync`/`DeployStackAsync`: the in-process DevChain + bundler, and the AA contract stack + demo contracts + funded paymaster deployed on top of it. `DeployStackAsync` also runs against an External node/bundler pair. Driven on demand by tab0 via `EmbeddedInfrastructureProvisioner`/`ExternalInfrastructureProvisioner`; also used for one-shot bring-up by the test fixtures. Shared with the Avalonia head. |
| `Components/Tabs/InfrastructureTab.razor` | Tab0: the *Deploy* button, a live log, and the resulting addresses once `SessionState.IsReady`. |
| `Components/Tabs/*.razor` | One component per tab, each `@inject`-ing its view model and binding to its `RelayCommand`s/observable properties. |
| `Components/Layout/MainLayout.razor` | Shows the shared `AccountBannerViewModel.DisplayText` (address, description, Counterfactual/Deployed, balance) in the page header. |
| `Components/ObservingComponentBase.cs` | Shared `PropertyChanged` -> `StateHasChanged` re-render plumbing so every tab re-renders when its view model changes. |

## What's out of scope here

Plain Blazor only - no MudBlazor, no wallet-UI packages - so this doubles as a minimal template for
an app that wants Account Abstraction without a full wallet UI.
