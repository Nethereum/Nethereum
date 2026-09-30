# Nethereum Account Abstraction — E2E reference example

A small, tabbed reference app showing how to use ERC-4337 Account Abstraction through
Nethereum's on-ramp (`AddNethereumAccountAbstraction` + `IAAClient`) with the
same idiom you already use for `Web3.Eth` and typed contract services. It is deliberately built
as a **head-agnostic MVVM core** so the identical view models drive both a Blazor and an Avalonia
head — and so the whole thing doubles as a **template** for apps that want AA without the full
wallet UI.

## Project layout

The example is split into siblings so the shared logic, its tests, and each UI head are cleanly
separated — the shape a real app should copy:

| Project | What it holds |
|---|---|
| `Nethereum.AccountAbstraction.Example.Core` | Head-agnostic view models (one per tab, tab0 included), the mutable `SessionState` infra holder, and the DI registration (`AddExampleCore`). No UI framework, no bundler/infra addresses at startup — `SessionState` starts empty and tab0 publishes into it on demand. Depends on Nethereum client packages — `Nethereum.AccountAbstraction` (+ its WebAuthn package `Nethereum.AccountAbstraction.WebAuthn`) and `Nethereum.Web3` — plus the sibling `Example.Contracts` project; no UI framework. |
| `Nethereum.AccountAbstraction.Example.Core.Tests` | Descriptive **BDD tests as documentation** — each `Given/When/Then` drives a view model against a **real in-process bundler** (validation ON) via `Nethereum.AccountAbstraction.Bundler.InProcess`, never a mock. Read these to learn the flows. |
| `Nethereum.AccountAbstraction.Example.Hosting` | `HostBootstrap.StartInfraAsync`/`DeployStackAsync` (the in-process DevChain + bundler, and the AA contract stack + demo contracts + funded paymaster deployed on top) plus the DI registrations every head calls: `AddExampleHostDeferred()` (a running head - nothing deployed yet, tab0 deploys on demand) and `AddExampleHost(infra)` (one-shot bring-up, for test fixtures). No UI framework. |
| `Nethereum.AccountAbstraction.Example.Blazor` | Blazor Server head: thin views bound to the Core view models. `dotnet run` needs no external node, and deploys nothing until you click Deploy on tab0. |
| `Nethereum.AccountAbstraction.Example.Avalonia` | Avalonia desktop head: the same view models and the same `Example.Hosting` bootstrap, native Avalonia views with compiled bindings; every tab past tab0 is gated on `SessionState.IsReady`. |
| `Nethereum.AccountAbstraction.Example.Avalonia.Tests` | Headless UI-wiring smoke tests (`Avalonia.Headless.XUnit`) - each tab View, resolved from the real bootstrapped host, renders and its buttons bind to the exact `[RelayCommand]` instances on its view model. |

The view models raise plain `Action` events (not `EventCallback`), so nothing in Core is tied to
a UI framework; each head wires those events to its own rendering.

## The tabs (view models in Core)

- **Setup infra** (`InfrastructureSetupViewModel`) — tab0: deploys the AA stack on demand (nothing
  runs at app startup). Nethereum ships a full ERC-4337 bundler
  (`Nethereum.AccountAbstraction.Bundler`), not just a client, and this tab offers three ways to run
  it: **Embedded** brings that bundler up in-process against an in-process DevChain for zero-setup
  testing; **External** points the SAME client + bundler API at a node/bundler you already run
  (test-local vs real infra is just a different endpoint plus a funder key); **Use existing** points
  that same client at ANY provider's already-deployed standard ERC-7579 modules — client-side
  interop testing — deploying only this demo's own target contracts. Every other tab is gated on
  `SessionState.IsReady` until this finishes.
- **Setup** (`SetupViewModel`) — create a fresh modular smart account (counterfactual CREATE2
  address; it deploys itself on the first operation), choosing a per-account payment mode
  (self-funded or paymaster-sponsored — see Gasless below).
- **Send** (`InteractionViewModel`) — drive a typed contract service through AA; happy path plus
  a reverting-call failure path rejected by the bundler's pre-flight gas estimation.
- **Batch** (`BatchViewModel`) — several calls as one atomic `UserOperation` (ERC-7579 batch);
  all-or-nothing on a failing call.
- **Gasless** (`GaslessViewModel`) — a `VerifyingPaymaster` sponsors the gas so the account
  never pays; the account's ETH balance stays exactly zero. The same sponsorship can also be chosen
  per-account on Setup/Passkey, applied automatically to every action tab from then on; it is
  unavailable in use-existing infra mode, since no provider ships a paymaster alongside its
  standard modules.
- **Custom Contract** (`CustomViewModel`) — drive an app-specific contract through AA and decode
  its typed custom errors on a rejected call.
- **Passkey** (`PasskeyAccountViewModel`) — a WebAuthn/P-256 passkey owns and signs for the
  account (the signer axis, no ECDSA key).
- **EIP-7702** (`Eip7702AccountViewModel`) — upgrade an existing EOA in place into a smart account
  and interact with it.
- **Modules** (`ModulesViewModel`) — install/uninstall an ERC-7579 validator on the active
  account via a self-signed `UserOperation`.
- **Policies** (`PoliciesViewModel`) — a SmartSession session key scoped by policy
  (SudoPolicy / UniActionPolicy), including an out-of-policy rejection.
- **Workflows** (`WorkflowsViewModel`) — an OwnableExecutor lets a delegated agent act on the
  account without signing each op; an unauthorized caller is rejected.
- **Social Recovery** (`SocialRecoveryViewModel`) — a guardian quorum rotates the account owner
  after a "lost key"; an under-threshold attempt is rejected.
- **Diagnostics** (`DiagnosticsViewModel`) — a developer view of a `UserOperation`'s gas, packed
  wire form, receipt and failure/out-of-gas diagnostics.

Each tab is one small view model over the on-ramp — the point is that AA is used the same way as
any other typed Nethereum call.

## Run the local stack as real servers

Tab0's three infra modes are three different topologies for the exact same client + bundler
API, not three different code paths:

- **Embedded** — the node and bundler run in-process, inside the demo itself. Zero setup, but
  everything is one process.
- **This scripted local stack** — `run-local-stack.ps1` starts Nethereum's *own*
  `Nethereum.DevChain.Server` and `Nethereum.AccountAbstraction.Bundler.RpcServer` as two real,
  separate HTTP processes on your machine, and the demo talks to them exactly like it would talk
  to real infra: client -> HTTP bundler -> HTTP node. It's the same code the Embedded mode uses
  under the hood, just running standalone. This is the easiest way to see that Nethereum ships a
  full, runnable ERC-4337 bundler — not just a client library — without needing any third-party
  tool (no anvil/Hardhat node, no reference bundler) or your own infra.
- **Your own external infra** — point tab0 -> External at any node + bundler you run yourself
  (your own deployment of `Nethereum.AccountAbstraction.Bundler.RpcServer`, or a third-party
  bundler if you're testing interop). Same three fields, same client code.

To run the scripted local stack:

```
pwsh src/demos/Nethereum.AccountAbstraction.Example/run-local-stack.ps1
```

The script is Windows only (it uses `taskkill`/`Get-NetTCPConnection` for process-tree teardown
and port checks); the servers themselves are cross-platform — on Linux/macOS run the two
`dotnet run` commands it wraps (`Nethereum.DevChain.Server` and
`Nethereum.AccountAbstraction.Bundler.RpcServer`) directly.

This starts the DevChain node, deploys an ERC-4337 EntryPoint onto it, builds and starts the
bundler pinned to that EntryPoint, and prints a summary block once both are answering JSON-RPC:

```
Paste into tab0 -> External:

  Node RPC url        : http://127.0.0.1:8545
  Bundler url         : http://127.0.0.1:4337/
  Funder private key  : 0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80
```

Paste those three values into tab0, choose **External**, and click Deploy — the demo deploys the
rest of the AA stack (factory, validator, paymaster, etc.) against the bundler's EntryPoint using
that funder key. The script then blocks, keeping both servers up for as long as you're using the
demo; Ctrl-C stops and cleans up both process trees.

Pass `-SmokeTest` to instead bring the stack up, verify it (node chain id, bundler chain id, and
that the bundler's `eth_supportedEntryPoints` reports the EntryPoint that was just deployed),
tear down, and exit non-zero on any failure — useful as a quick sanity check or in CI, without
needing to run the demo UI. See the script's own header comment for the full parameter list
(`-NodePort`, `-BundlerPort`, `-ChainId`).

## Running the tests

```
dotnet test src/demos/Nethereum.AccountAbstraction.Example/Nethereum.AccountAbstraction.Example.Core.Tests
```

They spin up an in-process DevChain + bundler per fixture, deploy the EntryPoint, the modular
account factory, the ECDSA validator, a demo counter contract and (for the gasless tab) a funded
`VerifyingPaymaster`, then exercise the real end-to-end flow. Serialized (no test parallelism) so
the shared in-process RPC is not contended.
