# Nethereum Account Abstraction Example - Avalonia desktop head

A runnable Avalonia desktop front end for the AA example's thirteen tabs: **Setup infra** (deploy
the AA stack on demand), **Setup**, **Send**, **Batch**, **Gasless**, **Custom Contract**,
**Passkey** (WebAuthn), **EIP-7702** (upgrade an EOA in place), **Modules** (ERC-7579
install/uninstall), **Policies** (SmartSession session keys scoped by policy), **Workflows**
(ERC-7579 `OwnableExecutor` - a delegated automation agent acts on your account without your
account signing each operation), **Social Recovery** (ERC-7579 `SocialRecovery` - an N-of-M
guardian quorum rotates the account's owner after a simulated "lost key", with an under-threshold
quorum rejected on-chain), and **Diagnostics** (a developer/observability view of a UserOperation -
the same `count()`/`countFail()` calls other tabs use, run to a genuine success, a genuine on-chain
revert, and a genuine out-of-gas failure, with the real receipt, gas, bundler lookup and
packed-wire data behind each) - the exact same view models the Blazor head binds to
(`Nethereum.AccountAbstraction.Example.Core`), with native Avalonia views (compiled bindings) instead
of Razor components. There is no AA logic or raw ABI handling in this project.

## Running it

```
dotnet run --project src/demos/Nethereum.AccountAbstraction.Example/Nethereum.AccountAbstraction.Example.Avalonia
```

`dotnet run` needs no external node, bundler, or configuration - and deploys nothing until you ask
it to: `MainWindow` shows immediately, landing on **0. Setup infra**, with every other tab disabled
until you click *Deploy* there. Nethereum ships a full ERC-4337 bundler
(`Nethereum.AccountAbstraction.Bundler`), not just a client, and tab0 offers three modes for running
it, same as the Blazor head: **Embedded** (default) brings up an in-process DevChain plus an
in-process bundler with ERC-4337 validation ON (the same `HostBootstrap.StartInfraAsync`/
`DeployStackAsync` from `Nethereum.AccountAbstraction.Example.Hosting`), then deploys the
EntryPoint/ECDSAValidator/NethereumAccountFactory stack, the demo `TestCounter`/`BookingRegistry`
contracts, and a funded sponsoring `VerifyingPaymaster`. **External** deploys the same stack against
a node + bundler you already run, given their RPC urls and a funder private key. **Use existing**
points that same client + bundler-API at a PROVIDER's already-deployed standard ERC-7579 modules
instead - client-side interop testing - and deploys only this demo's own target contracts through
the funder; there is no paymaster in this mode, so **Gasless** is unavailable. Watch tab0's own log
for progress.

A header banner above the tabs (`AccountBanner`, bound in `MainWindow.axaml.cs` to a resolved
`AccountBannerViewModel`) shows whichever account is currently active: its address, who/what controls
it, whether it has deployed, and its DevChain balance - the same shared view model the Blazor head's
`MainLayout.razor` renders.

## Click-through order

Same as the Blazor head: **Setup infra** (click *Deploy* - nothing else works until this finishes)
&rarr; **Setup** (pick a payment mode - self-funded or paymaster-sponsored, if configured - create
the account, then *Fund account* if self-funded - a DevChain-only faucet button, not an AA concept,
that funds whichever account is currently active) &rarr; **Send** &rarr;
**Batch** &rarr; **Gasless** (works even unfunded - the same sponsorship Setup/Passkey can also
choose per-account) &rarr; **Custom Contract** &rarr; **Passkey**
(pick a payment mode, create a passkey-owned account via Windows Hello, fund it if self-funded, send
a passkey-signed count) &rarr;
**EIP-7702** (generate and fund a plain EOA, then upgrade it in place and send - same address, now
a smart account) &rarr; **Modules** (install the shared `WebAuthnValidator` as a second validator
on the active account, prove it can sign, then uninstall it) &rarr; **Policies** (create a
SmartSession session key scoped to `count()` and a capped `gasWaster`, use it, then see an
out-of-policy call rejected) &rarr; **Workflows** (create an automation agent, grant it
`OwnableExecutor` rights, watch it call `count()` on your behalf directly, see an unauthorized EOA
rejected, then revoke it) &rarr; **Social Recovery** (install `SocialRecovery` with a fresh 2-of-3
guardian set, simulate losing your owner key and let a guardian quorum recover the account to a new
owner key, switch the active account over to it and prove the new key genuinely controls it, then
see an under-threshold quorum rejected on-chain with the owner left unchanged) &rarr; **Diagnostics**
(run *Run diagnosed op (success)* first - it deploys the account the other two flavors need - then
*Run reverting op* and *Run out-of-gas op* to see the same receipt/gas/bundler-lookup/packed-userop
panels populated for a genuine on-chain revert and a genuine out-of-gas failure). See the top-level
example README and the Blazor head's README for the full walkthrough.

## Project shape

| File | Role |
|---|---|
| `Program.cs` / `App.axaml.cs` | Builds a `ServiceProvider` via `AddExampleHostDeferred()` (empty `SessionState` + the embedded infra provisioner) and shows the real `MainWindow` immediately - no deploy, no splash. |
| `MainWindow.axaml(.cs)` | A `TabControl` hosting tab0 plus the twelve `Views/*View` controls, every tab past tab0 with `IsEnabled`/`ToolTip` bound straight to `SessionState.IsReady`; code-behind binds the `AccountBanner` panel to a resolved `AccountBannerViewModel` singleton, and assigns each tab's resolved view model as that view's `DataContext`. |
| `Views/InfrastructureView.axaml(.cs)` | Tab0: the *Deploy* button, a live log, and the resulting addresses once `SessionState.IsReady`. |
| `Views/*.axaml(.cs)` | One `UserControl` per tab, each with `x:DataType` set to its Core view model so bindings are compile-time checked. Buttons bind to `[RelayCommand]`-generated commands, including Setup's and Passkey's Fund buttons - there is no code-behind logic in any tab view. |
| `Converters/StringToBigIntegerConverter.cs` | Parses the Custom Contract tab's slot-number `TextBox.Text` into the `BigInteger` the Book/Release/GuestOf commands take - view-binding plumbing, not AA logic. |

## What's out of scope here

No AA/backend code lives in this project - it only consumes `Example.Core`'s view models and
`Example.Hosting`'s bootstrap, exactly like the Blazor head.
