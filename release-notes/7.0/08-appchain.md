# AppChain — Nethereum 7.0

Nethereum 7.0 turns the AppChain into a run-anywhere, application-specific chain you operate yourself: a centralised sequencer produces blocks over a full EVM, exposes a standard Ethereum JSON-RPC surface, enforces who may write through transparent on-chain-verifiable policy, and anchors its state roots to any L1/L2 so the whole history stays tamper-evident and independently verifiable. It ships as a single `dotnet tool` (`nethereum-appchain`) that runs in one of three roles — **sequencer**, **follower**, or **Clique validator** — and, for 7.0, the AppChain drops its own peer-to-peer and sync code and composes on the shared `Nethereum.DevP2P` / `Nethereum.DevP2P.Sync` stack, so a follower cold-syncs and stays live over the same RLPx/snap engine mainnet uses. Two new packages land — `Nethereum.AppChain.Server.Core` (the composable host) and `Nethereum.Explorer.Anchoring` (a pluggable anchoring explorer UI) — alongside a from-source overhaul of the core, sequencer, policy and anchoring packages, and a new Foundry contract suite (`Nethereum.AppChain.Contracts`). This is a preview release (`7.0.0-preview`): every capability below is exercised by a passing test, but interfaces may still move before 7.0 final.

## Nethereum.AppChain — Chain Foundation & Genesis

The `IAppChain` abstraction: a chain with blocks, transactions, a full EVM, and Patricia state roots, over pluggable storage (in-memory, RocksDB, or custom `IBlockStore`/`IStateStore`). It builds a genesis block, pre-funds accounts, pre-deploys the canonical CREATE2 factory, and optionally deploys the MUD World framework — the shared chain state that the sequencer produces and followers verify.

* `IAppChain` — one interface over `Blocks`/`State`/`Transactions`/`Receipts`/`Logs`/`TrieNodes`, plus `GetBalanceAsync`/`GetNonceAsync`/`GetCodeAsync`/`GetStorageAtAsync`/`GetAccountAsync` and block/receipt/transaction lookups; `InitializeAsync` validates an existing genesis or applies a fresh one, and is idempotent
* `AppChainGenesisBuilder` — applies pre-funded accounts, computes the state root via the Patricia trie, persists the trie nodes so the root is walkable, and encodes the header; a pre-funded address that is also a predeploy keeps its code and nonce
* `Create2FactoryGenesisBuilder` — pre-deploys the canonical CREATE2 factory at `0x4e59b44847b379578588920cA78FbF26c0B4956C` and computes deterministic addresses (`CalculateCreate2Address`) for counterfactual (ERC-4337) deployment
* `GenesisOptions` — `PrefundedAddresses`, `PrefundBalance`, `DeployCreate2Factory`, optional MUD World deployment; `ApplyGenesisStateAsync` bootstraps a follower's state so its first imported block's root matches
* Incremental state root — a block after genesis walks only that block's touched accounts and still matches a full recompute; no account is left dirty after genesis completes
* Data survives restart under RocksDB storage

## Nethereum.AppChain.Sequencer — Block Production & Validation

The centralised operator: it accepts transactions, validates them through a fixed pipeline, orders them into blocks, and produces at a configurable interval or on demand. It fronts pluggable block-production strategies (single-sequencer or Clique PoA) and, new for 7.0, a fencing-token lease authority for high-availability multi-node operation.

* `Sequencer` — start/stop, `SubmitTransactionAsync`, `ProduceBlockAsync`, a `BlockProduced` event, and an on-demand mode that seals immediately on submit (interval mode does not); all collaborators default from `SequencerConfig` when not supplied
* Validation pipeline — `PolicyEnforcer` first (signature, allowlist and calldata-size checks, reported as `PolicyViolationType.InvalidSignature` / `UnauthorizedSender` / `CalldataTooLarge`), then sender recovery, the per-sender pool limit, intrinsic gas, nonce and balance checks, each rejecting with a specific error
* `PolicyEnforcer` — allowlist and calldata-size enforcement, case-insensitive address matching, live `UpdatePolicy`/`UpdateWritersRoot`
* High-availability lease authority — a fencing-token lease (`ISequencerArbiter.TryAcquireOrRenewAsync`/`ReleaseAsync`; acquire and renew are one call) grants exactly one producer at a time; an expired lease yields a strictly higher token, a stale-token release is a no-op, and a holder that loses the lease between blocks is gated from sealing (the height does not advance) — with non-vacuity twins proving a bypassed gate would double-seal
* `AppChainNode : ChainNodeBase` — wraps `IAppChain` with an optional sequencer, exposing `CanAcceptTransactions`, `SendTransactionAsync`, `GetPendingTransactionsAsync` and `ProduceBlockAsync`
* EIP-7928 block access lists — an Amsterdam sequencer retains the list its header commits to; a Prague sequencer retains none (the read is resource-not-found)

## Nethereum.AppChain.Policy — Access Control & Governance

Operator-controlled, transparently verifiable authorisation: who can write, what they can write, and how much. Policies start as local configuration and optionally migrate to an L1 smart contract for decentralised governance, verified on-chain through Merkle proofs against a stored root.

* Merkle-tree authorisation — `PolicyMigrationService.ComputeMerkleRoot`/`ComputeMerkleProof`/`VerifyMerkleProof` and `PrepareMigrationData` for gas-efficient on-chain verification of large allowlists (writers, admins, and a separate blacklist tree)
* `BootstrapPolicyService` — local writers/admins with live `AddWriter`/`RemoveWriter`/`AddAdmin`/`RemoveAdmin`, open-access and empty-list modes, case-insensitive matching
* `EvmPolicyService` — reads the current policy, roots and epoch from the L1 contract (and returns local config without an RPC), validating a writer against an allowlist plus Merkle/blacklist proofs
* `PolicySyncWorker` — an `IHostedService` that detects epoch changes on L1, refreshes the cached policy, and raises `OnPolicyUpdated`
* Bootstrap → L1 migration path with epoch-versioned atomic updates

## Nethereum.AppChain.Anchoring — L1/L2 State Anchoring & Cross-Chain Messaging

Commits state roots, block hashes, and optionally compressed block data or ZK proofs to an on-chain anchor contract on any EVM chain, making the AppChain's history verifiable from L1/L2. Anchoring is configured on two orthogonal axes — data availability × proof mode — with one strategy class per valid combination, and the same package carries the cross-chain messaging (Hub) pipeline.

* Seven anchoring strategies (`AnchoringStrategyFactory.Create(AnchoringDataAvailability, AnchoringProofMode)`) spanning `None`/`Calldata`/`BlobReference` DA × `None`/`StarkHash`/`SnarkOnChain` proof — from `AnchoringStrategy_NoDA_NoProof_CommitmentOnly` (cheapest) to `AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA`; pointless combinations (`BlobReference+None`, `BlobReference+StarkHash`) are rejected at creation
* `AnchorWorker` (`IHostedService`) + `AppChainAnchorBatchService` — every `AnchorCadence` blocks, reads a block over standard RPC (`RpcChainAnchorable`), builds the proof payload via the strategy, and submits through `IAnchorService`, with retry/backoff
* On-chain guarantees enforced by `AppChainAnchor.sol` — chain continuity (`startBlock == prev.endBlock + 1`, `previousAnchorHash == prev.endBlockHash`), operator access control, proof-system minimum ("graduation") gating, and `IVerifier.verify` for SNARK-on-chain — all round-tripped from the typed client
* Restart recovery — `InitializeAsync` reads `getLatestAnchor` and restores `_lastEndBlock`/`_lastEndBlockHash` so the first post-restart anchor satisfies the continuity check
* Ad-hoc per-block proofs (`AppChainProofManager.sol` and its generated `AppChainProofManagerService`) — `submitBlockProof`, bonded `requestBlockProof`/`fulfillBlockProof`, independent of the anchoring cadence
* Brotli block compression via `CompressedEnvelope.Wrap`/`Unwrap` (`[version][algo][data]`, from `Nethereum.CoreChain`)
* Cross-chain messaging — a Merkle-accumulator message queue and processor (`IMessageQueue`, `MessageProcessor`, `MessagingService`) with contiguous/duplicate-id rejection, inclusion proofs, and a Hub send→poll→process→acknowledge→verify round trip against the L1 Hub contract
* `Nethereum.AppChain.Anchoring.Postgres` — an EF-based indexer (entities, repositories, event processing) that indexes `AnchorSubmitted` events for the explorer

## Nethereum.Explorer.Anchoring — Pluggable Anchoring Explorer UI

New package: a Razor Class Library that plugs anchoring pages into the Nethereum Explorer via `AddAdditionalAssemblies`, so an explorer host gains an anchoring dashboard, per-chain detail, and admin views without a bespoke build.

* Blazor pages — `AnchoringDashboard`, `AnchoringChainDetail`, `AnchoringAdmin`
* `IAnchorExplorerService` / `AnchorExplorerService` (with a `NullAnchorExplorerService` default) and an `AnchorExplorerNavContributor` that contributes navigation into the host explorer
* `AddAnchorExplorerServices` service-collection wiring so the pages are discoverable as an additional assembly

## Nethereum.AppChain.Server / Nethereum.AppChain.Server.Core — The AppChain Executable

The `nethereum-appchain` `dotnet tool` and its composable core (`Nethereum.AppChain.Server.Core`, new for 7.0). One process gives you block production, HTTP + WebSocket JSON-RPC, DevP2P peering/sync over the shared stack, Clique PoA, cross-chain messaging, L1 anchoring, and optional MUD deployment — in a role chosen by the keys you hand it.

* Three roles from configuration — **sequencer** (private key, produces blocks), **follower** (addresses only, imports and verifies over DevP2P via `AppChainDevP2PFollower`, never produces), **Clique validator** (one signer of a PoA set); role composition is verified against the production object graph, not a hand-rolled one
* Zero-config dev mode — with no identity flags the server generates ephemeral in-memory keys, logs `AppChain dev mode`, and starts producing; supplying a partial identity (e.g. only a genesis-owner key) fails cleanly rather than masking with ephemeral keys
* Config validation up front — follower mode requires both `--genesis-owner-address` and `--sequencer-address`; Clique requires a signer key (unless the node follows another node), initial signers and a reachable peer, and refuses `Sync.Mode` `None` — each enforced with a specific startup error
* Layered configuration — `appsettings.json` → `AppChain__` env vars → CLI flags (later wins), with raw `--AppChain:<Path>` expert overrides and `--help-advanced`; stable DevP2P node identity via `--node-key-hex`/`--node-key-file`
* HTTP surface — JSON-RPC `/` (`eth_*`/`web3_*`/`net_*`/`admin_*`), `/ws` subscriptions, `/status`, `/blocks/*`, `/finality/*`, `/health`
* Embeddable — `AppChainServerRunner.RunAsync(config)` is the whole server in one call; `AddAppChainServer` + `AddAppChainMetrics`/`AddAppChainOpenTelemetry`/`AddAppChainHealthChecks` wire the building blocks into your own host
* Multi-node Clique — three signers linked only by trusted peers converge on one chain sealed by multiple signers, import each other's blocks as parents, and an isolated signer never converges
* End-to-end scenarios — MUD gaming world (deploy, table read/write, ownership transfer), whitelisted-enterprise access control (member add/remove, operator always-accepted), and open/social chains, driven through the composed node

## Nethereum.AppChain.Contracts — Solidity Anchor & Policy Suite

The Foundry contract suite the on-chain side of the AppChain deploys (Solidity source + Foundry tests; part of the subsystem, not a NuGet package). It defines the anchor, hub, policy and proof-manager contracts and the pluggable authority/verifier interfaces the C# strategies target.

* `AppChainAnchor.sol`, `AppChainHub.sol`, `AppChainPolicy.sol`, `AppChainProofManager.sol`
* `IAuthority` / `IVerifier` / `IZiskVerifier` seams with example authorities (`SimpleAuthority`, `MultisigAuthority`, `StakeWeightedAuthority`, `DAOAuthority`, `CliqueAuthority`) and verifiers (`StarkBlobCommitmentVerifier`, `CalldataFormatVerifier`, `CalldataStarkVerifier`, `PipelinePayloadVerifier`, `ZiskVerifierAdapter`)
* The C# `AnchoringOnChainProofSystem` enum mirrors the Solidity `ProofSystem` enum byte-for-byte

## Removed / consolidated

For 7.0 the AppChain no longer ships its own peer-to-peer and sync implementation. It now composes on the shared `Nethereum.DevP2P` / `Nethereum.DevP2P.Sync` stack — the same RLPx transport and snap-first sync engine mainnet uses — so an AppChain follower cold-syncs and stays live over the shared engine, and there is one networking stack to maintain and reason about instead of two. `Nethereum.AppChain.Server.Core` references `Nethereum.DevP2P.Sync` directly, and `AppChainDevP2PFollower` drives it.

The following packages that existed at 6.1.0 are **gone** at 7.0:

- **`Nethereum.AppChain.P2P`** — removed
- **`Nethereum.AppChain.P2P.DotNetty`** — removed
- **`Nethereum.AppChain.P2P.Server`** — removed
- **`Nethereum.AppChain.Sync`** — removed
- **`tests/Nethereum.AppChain.SyncPerformanceTests`** — removed (the test project for the above)

If you referenced any of these, switch to the shared DevP2P stack: run a follower with the `nethereum-appchain` tool in follower mode (`--follow-peer <enode>` plus the trusted `--genesis-owner-address`/`--sequencer-address`), enable serving on a producer with `--enable-devp2p-serve`, or embed `Nethereum.DevP2P.Sync` directly. The Docusaurus package-reference pages for the removed projects and the now-empty "Networking" sidebar category were removed with them.
