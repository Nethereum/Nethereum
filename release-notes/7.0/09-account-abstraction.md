# Account Abstraction — Nethereum 7.0

Nethereum 7.0 extends the ERC-4337 / ERC-7579 Account Abstraction stack that shipped in 6.1.0 — the client, bundler, RocksDB stores and RPC server — with a one-call on-ramp, an in-process bundler host, a passkey/WebAuthn signer stack, multi-guardian social recovery, in-place EIP-7702 EOA upgrade, and ERC-7562 / EIP-7623 hardening. You can now create and drive modular smart accounts, sponsor gas with paymasters, scope session keys by policy, recover an account through guardians, upgrade an EOA in place with EIP-7702, and run the whole thing against Nethereum's own in-process or standalone ERC-4337 bundler — no third-party bundler required. This is a preview release (`7.0.0-preview`): the capabilities below are exercised by on-chain or unit tests (the ERC-7562 compliance suite runs locally and is not yet CI-wired), and interfaces may still move before 7.0 final.

## New packages

- **`Nethereum.AccountAbstraction.Bundler.InProcess`** — hosts the ERC-4337 bundler in the same process as your app (`InProcessBundlerHost`) instead of a separate server, for tests and single-process deployments. Backs most of the modular-account E2E suites.
- **`Nethereum.WebAuthn`** (+ **`.Blazor`**, **`.Windows`**) — a standalone passkey/WebAuthn stack: credential creation and assertion, a software authenticator for tests, and an `IAccountSigningService` (`WebAuthnAccountSigningService`) so a passkey signs UserOperations like any other signer. `.Blazor` drives real browser passkeys via `navigator.credentials` (`BlazorWebAuthnAuthenticator`); `.Windows` drives Windows Hello via `webauthn.dll` (`WindowsWebAuthnAuthenticator`).
- **`Nethereum.AccountAbstraction.WebAuthn`** — wires the passkey signer into an ERC-7579 validator module (`WebAuthnValidatorModule`) so a modular smart account can be owned, or co-owned, by a passkey, with verification routed through the EIP-7951 `P256VERIFY` precompile.

## Smart accounts & modules (ERC-7579)

- Modular `NethereumAccount` deployment and execution — batching, module install/uninstall, and session-key–scoped calls.
- **SmartSession** session keys with spending-limit and action policies (`SmartSessionConfig`, `ERC20SpendingLimitBuilder`) — a session can be capped to specific ERC-20 amounts/actions, including N-of-M multi-signer session authority enforced on-chain.
- **Social recovery** via a multi-guardian (N-of-M) validator module (`SocialRecoveryValidatorModule`) — guardians co-sign a recovery operation that replaces the account's owner.
- **`OwnableExecutor`** delegated actions — install a second account's executor module post-deployment and drive an action through it via the account's own self-call.
- Signer-agnostic ownership — any `IAccountSigningService` (ECDSA, external signer, or WebAuthn) can own a modular account.

## Instant on-ramp

- `services.AddNethereumAccountAbstraction(o => ...)` + `IAAClient.CreateAccountAsync` takes an owner key to a landed UserOperation on a modular account in a few lines, and `UseAccountAbstraction` switches any generated typed contract service onto AA so ordinary typed calls route through the smart account.

## EIP-7702 — upgrade an EOA in place

- `CreateEip7702Account` + `ConfigureEip7702` upgrade a plain EOA into a delegated smart account without moving funds to a new address; `AAContractHandler.WithEip7702Delegation` gives the same capability at the lower level, including installing a validator on delegation.

## Paymasters & batching

- Deposit-based paymaster sponsorship — ERC-20 transfers, batched calls, multiple sequential sponsored operations, and the failure paths (insufficient deposit, deposit-balance tracking, gas-accounting validation) — proven over real bundler round trips against a test accept-all paymaster; `VerifyingPaymasterManager` supplies verifying-paymaster signature handling and is exercised in the full smart-account lifecycle test.
- Atomic multi-call batching through the ERC-7579 execution encoding (`ERC7579ExecutionLib.EncodeBatch`) — several contract calls in a single UserOperation, including fully paymaster-sponsored batches to multiple recipients.

## Bundler

- A standalone ERC-4337 bundler RPC server (`Nethereum.AccountAbstraction.Bundler.RpcServer`), proven over a real HTTP round trip from the typed client to a UserOperation receipt, plus the in-process host above.
- RocksDB-persistent mempool and reputation store (`RocksDbUserOpMempool`, `RocksDbReputationStore`) — a restarted bundler keeps its pending operations and entity reputation.
- ERC-7562 bundler-side validation (banned opcodes, storage-access rules, staking/reputation gates) enforced against real execution traces (`ERC7562RuleEnforcer`), with the eth-infinitism `bundler-spec-tests` suite runnable locally as a `dotnet test` (requires anvil + the spec-tests venv; not yet CI-wired).
- Simulation-based gas estimation (`UserOperationGasEstimator`) that simulates rather than guesses `callGasLimit` (paymaster gas limits use fixed fallbacks in the estimator; `AAContractHandler` adopts the limits returned by the bundler's estimate), with EIP-7623 floor pre-verification gas (`Eip7623PreVerificationGasCalculator`).

## Reference demo apps

Two runnable sample apps under `src/demos/` (source-only reference apps, not NuGet packages) show the stack end to end:

- **`Nethereum.AccountAbstraction.Example`** — a tabbed AA reference app built as a head-agnostic MVVM core, so the same view models drive both a **Blazor** and an **Avalonia** head; it doubles as a template for AA apps. Tabs walk the full stack — on-demand infra setup with three bundler topologies (embedded in-process, a scripted local two-server stack via `run-local-stack.ps1`, or your own external node + bundler), modular account setup, send / batch / custom-contract calls, gasless `VerifyingPaymaster`, passkey-owned accounts, EIP-7702, module install/uninstall, SmartSession policies, OwnableExecutor workflows, social recovery, and a UserOperation diagnostics view. Its `*.Core.Tests` are BDD `Given/When/Then` tests driving each view model against a real in-process bundler.
- **`Nethereum.AccountAbstraction.AppChain.Enterprise.Example`** — an enterprise AppChain AA demo (Blazor + Avalonia heads over a shared MVVM core, with `DeployEntryPoint` and hosting, built on `Nethereum.AccountAbstraction.AppChain`) covering the full enterprise smart-account lifecycle, not just onboarding: admin-directory-driven, paymaster-sponsored enrollment of an account per `userId` (`EnterpriseDirectoryAdminService`), capped SmartSession operator roles, N-of-M (2-of-3) tiered approval for large spends, and offboarding (owner rotation via social recovery, session revoke, module uninstall, fund sweep to treasury, and registry ban).
