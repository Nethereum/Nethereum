# Nethereum 7.0.0

Nethereum 7.0 evolves Nethereum into a full Ethereum SDK and node toolkit for .NET.

Developers can now work with Ethereum at every level: interact through JSON-RPC, embed the EVM, execute and simulate transactions and blocks, connect directly to Ethereum over DevP2P, verify Ethereum through a beacon light client, follow mainnet, and, in the future, extend Ethereum with application-specific chains that remain anchored to Ethereum through L1 or L2s.

The goal is to make more of Ethereum itself directly usable from .NET — not only the APIs exposed by a remote node, but the execution, networking, verification and chain-building components underneath them.

The EVM is rebuilt as one engine that runs in a live node, a transaction simulator and a zkVM guest, and executes every fork from Frontier through Amsterdam (EIP-8037 two-dimensional gas, EIP-8038 repricing, EIP-7928 block access lists, EIP-7708 ETH-transfer logs). On top of it ship a complete node storage-and-execution engine with a RocksDB backend and a geth-compatible freezer, native devp2p networking with snap sync, a trust-minimised beacon light client, and a shared hosting layer that runs a read-only mainnet follower, the AppChain and the DevChain from the same building blocks. The client stack gains the post-Prague / Amsterdam JSON-RPC surface (`eth_config`, `eth_simulateV1`, Engine API DTOs), the Account Abstraction stack gains passkey signers, social recovery and an in-process bundler, X402 moves to protocol v2 with Permit2, Ethereum blocks can be proven inside the Zisk zkVM, and new `Nethereum.DID` packages resolve `did:ethr` identities. The repository moves to `Nethereum.slnx`.

[Full Changelog](https://github.com/Nethereum/Nethereum/compare/6.1.0...7.0.0)

## Architecture

The diagram shows how the 7.0 packages compose, from the primitives at the bottom to the node hosts and applications at the top. You can use them at any level: the client stack against a remote node, the EVM embedded without a node, or a full node composed from the pieces in between.

```
 Applications & extensions   Account Abstraction · X402 · ENS · SIWE · DID · Uniswap · Circles · MUD
                             Indexing · Explorer · Solidity debugger · Wallet / UI · Unity
                                       │
 Client stack                JsonRpc · RPC · Web3 · Contracts · Accounts  ──►  any Ethereum node,
                                       │                                       including the hosts below
 Node hosts                  MainnetChain · AppChain · DevChain
                                       │  composed by
 Hosting                     ChainNode.Hosting   (storage + mempool + peer serving + sync → one ChainNode)
                                       │
          ┌────────────────────────────┼────────────────────────────┐
 CoreChain                     DevP2P · DevP2P.Sync          Consensus
 execute · import · produce    RLPx · discovery ·            beacon light client ·
 follow · JSON-RPC · Engine    snap sync · serving           Clique PoA gate
 storage: in-memory · CoreChain.RocksDB · Freezer
          └────────────────────────────┬────────────────────────────┘
                                       │
 EVM                         EVM.Core (stateless engine) · EVM.Precompiles · EVM (simulation & debugging) · EVM.Zisk (zkVM guest)
                                       │
 Foundations                 Model · RLP · SSZ · Signer · Util · Merkle
```

* **Foundations** — the block, transaction and wire data model, encodings, hashing, signing and tries every other layer uses.
* **EVM** — one engine compiled from one source tree: `Nethereum.EVM.Core` runs stateless from a witness, `Nethereum.EVM` hosts it for simulation and debugging over JSON-RPC, and `Nethereum.EVM.Zisk` runs it as a zkVM guest.
* **CoreChain** — the node engine that executes, validates, produces and follows blocks over the `IChainStoreBundle` storage interfaces, and serves `eth_*` JSON-RPC and the Engine API.
* **DevP2P** — the wire protocol and the sync engine that fill a CoreChain store from real peers.
* **Consensus** — what makes a followed chain trustworthy: the beacon light client for mainnet and the Clique gate for proof-of-authority chains.
* **Hosting** — `Nethereum.ChainNode.Hosting` composes those pieces into one `ChainNode`; the mainnet follower, the AppChain and the DevChain all compose from its building blocks.
* **Client stack and applications** — the established Web3 client, which talks to any node and which the EVM and CoreChain also use for RPC types and live-state reads, and the application packages built on it.

Each section below summarises one area; the linked note for that area lists the full API-level delta.

## Foundations — Model, Signer, Util, Merkle

The model, encoding, hashing, signing and trie primitives can now describe an Ethereum block end to end, from any fork's header down to its wire bytes.

* Per-fork block header codecs from Legacy through Amsterdam (`BlockHeaderCodecSelector`, `AmsterdamBlockHeaderCodec` for the 23-field header) and per-type transaction decoders
* EIP-4844 blob transactions (`Transaction4844`, `BlobEncoder`, `BlobGasCalculator` with the EIP-7918 reserve-price branch) and `Transaction4844Signer`
* EIP-7928 block access lists (`AccountChanges`, `BlockAccessListRLPEncoder`) and the devp2p / snap / LES wire message model with EIP-2124 `ForkId`
* `EvmUInt256` / `EvmInt256` / `EvmAddress` value types for allocation-free 256-bit EVM arithmetic, and a Poseidon2 hashing stack over Goldilocks and BN254
* Merkle-Patricia range proofs (`PatriciaRangeProofVerifier`), pluggable trie node stores, and EIP-7864 binary-trie state diffs
* ECDH / ECIES and ENR signing for networking, plus `EthECKeyBuilderFromSignedAuthorisation` for EIP-7702 authority recovery

Details: [Foundations](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/01-foundations.md)

## RPC & Web3 — The Post-Prague / Amsterdam JSON-RPC Surface

The client stack reaches the JSON-RPC methods that landed with Prague and Amsterdam, typed and composed like every other `web3.Eth.*` call.

* `web3.Eth.Config` (`eth_config`, EIP-7910), `web3.Eth.SimulateV1` (`eth_simulateV1`), `web3.Eth.Capabilities`, `web3.Eth.Blocks.GetBlockAccessList` (EIP-7928) and the `web3.TxPool` service
* Engine API DTOs (`ExecutionPayloadV1`–`V4`, `PayloadAttributesV1`–`V4`, `ForkchoiceStateV1`, …) checked field by field against the `execution-apis` OpenRPC spec
* EIP-7702 authorization lists signed by `AccountSignerTransactionManager`, and `AccountAbstractionAccount` for smart-account signing
* EIP-7708 ETH-transfer logs via `web3.Eth.EthTransfers`, and emitter-scoped event decoding (`DecodeEventEmittedBy`, `IsLogForEventEmittedBy`) so ETH movements are not mistaken for ERC-20 transfers
* ENS Universal Resolver (`web3.Eth.GetEnsUniversalResolverService()`) with SSRF-hardened CCIP-Read and ENSIP-21 batch gateways
* `RpcError.Code` accepts a string error code without throwing; `TransferEtherAsync` uses the node's gas estimate

Details: [RPC & Web3](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/02-rpc-web3.md)

## EVM — One Engine for Node, Simulator and zkVM, Through Amsterdam

You can execute a block or a transaction statelessly from a witness, replay a transaction against live chain state over plain JSON-RPC, and prove a block inside the Zisk zkVM — all from one source tree. Fork rules are chosen per block from Frontier through Amsterdam.

* New `Nethereum.EVM.Core` — a synchronous, AOT- and trim-safe stateless engine (`BlockExecutor.Execute`, `TransactionExecutor`, `HardforkRegistry`, `IChainActivations`, `IStateReader`)
* New `Nethereum.EVM.Precompiles` — the default managed crypto backends and `DefaultMainnetHardforkRegistry`; BLS12-381 (EIP-2537) and KZG (EIP-4844) plug in with `WithBlsBackend` / `WithKzgBackend`, and an unwired precompile throws `UnwiredPrecompileException` rather than returning a wrong answer
* Amsterdam executable and test-covered: EIP-8037 state-gas reservoir, EIP-8038 repricing, EIP-7928 block access lists, the four EIP-7002/7251/8282 request predeploys, EIP-7843, EIP-7708, EIP-8246, EIP-8024 and EIP-7954; Osaka BPO forks, EIP-7951 `P256VERIFY` and the EIP-7918 blob reserve price
* `Nethereum.EVM` simulation and debugging — `RpcNodeDataService` live-state replay, `ProgramResultDecoder` call trees and revert reasons, `StateChangesExtractor` balance changes, and `EVMDebuggerSession` source-level stepping

Details: [EVM (Amsterdam)](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/03-evm-amsterdam.md)

## Consensus — Beacon Light Client and Clique

You can bootstrap an Ethereum light client from a weak-subjectivity checkpoint, follow the beacon chain's finalized and optimistic heads with every sync-committee signature verified, and read a trusted execution header from it.

* `LightClientService` with per-period sync-committee verification, provenanced block-hash history, `TrustedHeaderProvider` staleness detection and `LightClientStateCodec` persistence
* Fork-aware consensus SSZ containers driven by one `ConsensusFork` value, with a `ChainSpec` slot→fork schedule through Fulu
* `CliqueConsensusBlockGate` refuses a block whose seal is not an authorised signer before it executes; `ICliqueProposalStore` holds validator votes
* Beacon REST light-client mapping and EIP-4844 blob-sidecar retrieval in `Nethereum.Beaconchain`

Details: [Consensus](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/04-consensus.md)

## CoreChain & Storage — The Node Engine and Freezer

Give `Nethereum.CoreChain` a block and it tells you whether it is valid and what state it produces; give it transactions and it seals a block; point it at a block source and it follows a chain to the tip.

* `BlockExecutor`, `BlockImporter` (seventeen self-naming validity checks), `BlockProducer`, `FollowerChainNode`, incremental state roots, EIP-1186 `ProofService` and reorg `RewindCoordinator`
* Full `eth_*` / `net_*` / `web3_*` JSON-RPC aligned with geth and execution-apis, including multi-block `eth_simulateV1`, `debug_traceTransaction` and Engine API handlers
* `Nethereum.CoreChain.RocksDB` — PBSS-style path-keyed state, bounded node history serving as-of proofs, split hot/history databases, boot recovery and write-pressure valves
* New `Nethereum.Freezer` — an append-only ancient store in go-ethereum's on-disk format, so a geth `ancient/` directory imports directly
* New `Nethereum.CoreChain.Freezer` — finality-gated promotion into the freezer and the EIP-7745 filtermaps log index serving `eth_getLogs` over frozen history

Details: [CoreChain & Storage](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/05-corechain-storage.md)

## DevP2P & Sync — Native Networking and Snap Sync (preview)

Nethereum can join the Ethereum wire network directly — dial and accept real peers, discover them, and sync a chain from scratch — with no external client in the loop.

* `Nethereum.DevP2P` — RLPx transport across `eth/68`–`eth/71` and `snap/1`, discv4, discv5 and EIP-1459 DNS discovery, dial scheduling and inbound admission
* `Nethereum.DevP2P.Sync` — the `SyncNode` facade: EIP-2124 fork-ID checks on every handshake, archive backfill, proof-verified snap sync with a rolling pivot, resumable checkpoints, flat-state verification, serving and transaction relay
* Snap/2 (EIP-8189) bootstrap with block-access-list catch-up, off by default (`BalHealEnabled`)

Details: [DevP2P & Sync](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/06-devp2p.md)

## Chain Hosting — Mainnet Follower (preview) and DevChain

One composition library, `Nethereum.ChainNode.Hosting`, turns storage, mempool, peer serving and sync into a single `ChainNode` from one `ChainNodeConfig`; the mainnet follower, the AppChain and the DevChain all compose from it.

* `ChainNode.StartAsync`, `ChainNodeConfigFactory` presets and `ChainNodeMempool` / `ChainNodeStorage` building blocks
* `Nethereum.MainnetChain` and the `nethereum-mainnetchain` dotnet tool — a read-only mainnet follower that snap-syncs, validates every block, gates on a beacon light client, and serves JSON-RPC; it refuses to start without a `--beacon` endpoint unless `--allow-unverified-consensus` is given
* `Nethereum.DevChain` reworked onto the shared hosting layer, with a JWT-authenticated Engine API endpoint, `debug_setHead`, mempool-style nonce admission and a `--rocksdb` storage mode

Details: [Chain Hosting](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/07-chain-hosting.md)

## AppChain — Application Chains on the Shared Stack (preview)

The AppChain runs as a single `nethereum-appchain` dotnet tool in a sequencer, follower or Clique validator role, and now composes on the shared DevP2P stack instead of its own networking code.

* `Nethereum.AppChain.Sequencer` validation pipeline and a fencing-token lease for high-availability producers
* `Nethereum.AppChain.Policy` Merkle-tree allowlists with a bootstrap → L1 migration path
* `Nethereum.AppChain.Anchoring` — seven data-availability × proof-mode anchoring strategies, restart recovery and cross-chain messaging
* New `Nethereum.AppChain.Server.Core` (the composable host) and `Nethereum.Explorer.Anchoring` (anchoring explorer pages)

Details: [AppChain](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/08-appchain.md)

## Account Abstraction — Passkeys, Recovery and an In-Process Bundler

You can create and drive modular ERC-7579 smart accounts, sponsor gas, scope session keys by policy, recover an account through guardians, upgrade an EOA in place with EIP-7702, and run it all against Nethereum's own bundler.

* New `Nethereum.AccountAbstraction.Bundler.InProcess`, `Nethereum.WebAuthn` (+ `.Blazor`, `.Windows`) and `Nethereum.AccountAbstraction.WebAuthn` (passkey-owned accounts via EIP-7951 `P256VERIFY`)
* `services.AddNethereumAccountAbstraction(...)` + `IAAClient.CreateAccountAsync` on-ramp, SmartSession policies, multi-guardian social recovery and `CreateEip7702Account`
* ERC-7562 rule enforcement, RocksDB-persistent mempool and reputation, and EIP-7623 floor pre-verification gas

Details: [Account Abstraction](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/09-account-abstraction.md)

## X402 — Protocol v2 and Permit2

`Nethereum.X402` moves to x402 protocol version 2, interop-verified against the coinbase/x402 reference implementation in both directions, and adds a Permit2 asset-transfer method that works with any ERC-20.

* v2 wire format (CAIP-2 networks, `PAYMENT-REQUIRED` / `PAYMENT-SIGNATURE` / `PAYMENT-RESPONSE` headers)
* `X402HttpClient` with a fail-closed `MaxAmount` and a `PaymentPolicy` allow-list checked before anything is signed
* `X402Middleware` serve-before-settle replay protection, pre-settle simulation and ERC-1271 smart-wallet signatures

Details: [X402](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/12-x402.md)

## ZK / Zisk — Proving Ethereum Blocks

`Nethereum.EVM.Core` cross-compiles to a RISC-V ELF that runs inside the Zisk zkVM, where a block witness executes and `cargo-zisk` turns the execution into a STARK proof.

* New `Nethereum.Zisk.Core` guest runtime bindings, plus the `Nethereum.Zisk.Prover.Server` and `Nethereum.BlockProver.Server` executables and the `zisk/` build pipeline in the repository
* `Nethereum.PrivacyPools` gains full 256-bit key derivation, migration-aware recovery and SHA-256 circuit-artifact integrity checks

Details: [ZK / Zisk](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/13-zk-zisk.md)

## Indexing — Amsterdam Data in the SQL Stack

* EIP-7928 block-access-list index with EF Core and in-memory backends, and a Postgres migration (`AddBlockAccessListAndAuthorizationList`) that upgrades a 6.1.0 database
* EIP-7708 native-transfer discrimination so a system-emitted `Transfer` log is never counted as a token balance
* `EvmReplayInternalTransactionSource` for internal transactions on nodes without `debug_traceTransaction`
* Amsterdam header fields and block-access-list data in `Nethereum.Explorer`

Details: [Indexing](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/14-indexing.md)

## DID — Decentralized Identifiers

* New `Nethereum.DID` — the W3C DID Core document model, a DID URL parser and Newtonsoft / System.Text.Json serialization
* New `Nethereum.DID.EthrDID` — `EthrDidResolver` resolves `did:ethr` identifiers from the ERC-1056 registry

Details: [ENS, MUD & DID](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/11-ens-mud-did.md)

## Wallet, UI and Unity

The wallet and UI libraries carry forward with fork-aware transaction simulation (`ChainForkResolver`), native ETH-transfer display and rebuilt READMEs; the Unity and MAUI packages carry forward unchanged apart from the `Nethereum.Unity.Metamask` package id.

Details: [Wallet & UI](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/10-wallet-ui.md) · [Unity & MAUI](https://github.com/Nethereum/Nethereum/blob/7.0.0/release-notes/7.0/15-unity.md)

## Bug Fixes

* **EIP-1186 proof verification is sound.** `StorageProofVerification` accepted an empty, root-only or root-less proof as a zero value for any slot, so an RPC could make a slot read as 0. Account and storage proofs now verify through `PatriciaProofVerifier.TryVerify`, the light client binds each proof to the exact address and slot requested, and `eth_getProof` returns geth's proof of absence for an absent account.
* **Verified storage reads use the right slot.** `VerifiedStateService.GetStorageAtAsync(address, BigInteger)` encoded the slot little-endian and zero-trimmed, so mapping and array slots read another slot's value; it now requests the 32-byte big-endian key EIP-1186 defines.
* **`EnsureHexPrefix` on a string array** now writes the `0x` prefix back onto each element; it previously returned the array unchanged (PR #1122).
* `Nethereum.Mud` decodes an empty `string` field to `""` instead of `null`.

## Package versions

Most packages ship as **7.0.0**. The new networking, mainnet-follower and AppChain packages ship as **7.0.0-preview**, so their interfaces may still move before they are declared stable:

* `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`
* `Nethereum.MainnetChain`, `Nethereum.MainnetChain.Server`
* `Nethereum.AppChain`, `Nethereum.AppChain.Sequencer`, `Nethereum.AppChain.Policy`, `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Server`, `Nethereum.AppChain.Server.Core`, `Nethereum.AccountAbstraction.AppChain`, `Nethereum.Explorer.Anchoring`

A stable package can depend on a preview one (for example `Nethereum.ChainNode.Hosting` on `Nethereum.DevP2P.Sync`), so restoring it pulls in that preview package.

New packages in 7.0: `Nethereum.EVM.Core`, `Nethereum.EVM.Precompiles`, `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`, `Nethereum.ChainNode.Hosting`, `Nethereum.MainnetChain`, `Nethereum.MainnetChain.Server`, `Nethereum.Freezer`, `Nethereum.CoreChain.Freezer`, `Nethereum.Zisk.Core`, `Nethereum.WebAuthn` (+ `.Blazor`, `.Windows`), `Nethereum.AccountAbstraction.WebAuthn`, `Nethereum.AccountAbstraction.Bundler.InProcess`, `Nethereum.AppChain.Server.Core`, `Nethereum.Explorer.Anchoring`, `Nethereum.DID`, `Nethereum.DID.EthrDID`.

## Breaking changes

* **Permit2 moved out of `Nethereum.Uniswap`.** The Permit2 service is now `web3.Eth.GetPermit2Service()` in `Nethereum.Contracts.Standards.Permit2`, the EIP-712 message types are in `Nethereum.ABI.EIP712.Permit2`, and `PermitSigner` is in `Nethereum.Signer.EIP712.Permit2`. The 6.1.0 helpers `GetSinglePermitWithSignatureAsync` / `GetBatchPermitWithSignatureAsync` and `SignedPermit2<T>` have no replacement: read the nonce with `Permit2Service.AllowanceQueryAsync` and sign with `PermitSigner.SignPermitSingle` / `SignPermitBatch`.
* **AppChain networking packages removed.** `Nethereum.AppChain.P2P`, `Nethereum.AppChain.P2P.DotNetty`, `Nethereum.AppChain.P2P.Server` and `Nethereum.AppChain.Sync` are gone; the AppChain now runs on the shared `Nethereum.DevP2P` / `Nethereum.DevP2P.Sync` stack.
* **`Nethereum.Unity.Metamask` package id.** 6.1.0 published it as `Nethereum.Unity.Metamasky`; update the `PackageReference`.
* **`VerifiedNodeDataService` implements the EVM `IStateReader`** instead of `INodeDataService`: results are `EvmUInt256`, `GetTransactionCount` became `GetTransactionCountAsync`, and `AccountExistsAsync` was added.
* **Signing defaults.** `EthECKey.GetPrivateKeyAsBytes()` now always returns the 32-byte scalar (a key with leading zero bytes was returned shorter in 6.1.0). On `net8.0`, `net9.0` and `net10.0`, `EthECKey.SignRecoverable` now defaults to `true`, so recoverable signing and public-key recovery run on NBitcoin.Secp256k1; set it to `false` to return to BouncyCastle.
* **Stricter decoding.** The legacy transaction decoder rejects a non-canonically encoded (leading-zero) scalar, and `SszMerkleizer.VerifyProof` requires a branch of exactly `depth` elements.
* **`ITransactionLogView.BlockTimestamp`** is a new interface member; custom implementations must add it.
* **JSON-RPC server behaviour (CoreChain / DevChain).** `eth_subscribe` refuses any subscription type other than `newHeads` and `logs` with `-32000`.
* **Client DTOs.** `EthGetUserOperationByHash` returns a `UserOperationByHashResult` wrapper, and `ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(chainId)` returns `null` for an unknown chain instead of assuming ether.

## Build & Packaging

* `Nethereum.slnx` replaces `Nethereum.sln`; the target-framework matrix is unchanged from 6.1.0.
* Every published package is strong-name signed with the shared key, and no project uses `InternalsVisibleTo`. Members that tests previously reached as internals are public.
* `Nethereum.MainnetChain` and `Nethereum.MainnetChain.Server` depend on `Nethereum.Signer.Bls.Herumi` at the release version instead of an unpublished 6.5.0.
* The pack scripts (`src/nuget.bat`, `src/nuget-fast.ps1`) pack the new 7.0 packages; `Nethereum.EVM.Zisk` and the prover-server executables are not published to NuGet.

## Documentation

Package READMEs across the library were reworked against their source, and the documentation site's package-reference pages for the removed AppChain networking packages were removed.

## Known limitations

* On mainnet the snap-sync flat-state reconcile runs before the node starts following and takes hours (about 4.5 hours on the test host).
* Snap/2 is not enabled on mainnet; the mainnet follower runs snap/1.
* Interop with go-ethereum's `devp2p` tool, the Zisk proving path and the ERC-7562 bundler spec tests run through local harnesses rather than continuous integration.
