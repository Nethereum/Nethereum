# Nethereum AppChain Integration — Umbrella Plan

**Date:** 2026-04-20 (updated 2026-04-23)
**Status:** A's foundation layers DONE (binary trie, Poseidon, pluggable interfaces, SSZ, EIP-4844 blobs, beacon client, DevChain blob storage). A's config surface + templates are NEXT. C/E specs drafted; G/D/B/F scope captured.

---

## Context

Two tracks converged in April 2026: the EVM.Core + Zisk zkVM pipeline (binary trie, Poseidon / SHA256 / Keccak / Blake3 hash providers, SSZ models, BLS12-381 / KZG / P256 precompiles validated through `ziskemu`), and the `Nethereum.AppChain.*` project family (`Sequencer`, `Sync`, `Server`, `P2P`, `Anchoring`, `Policy`, `Templates`, `Contracts`). This umbrella connects the two into a cohesive story for shipping **production-SOLID AppChains** — chains that are operationally robust, selectively proved, and aligned with where Ethereum is heading.

**Positioning:**

- `Nethereum.CoreChain` is the execution framework. It owns the full config surface (encoding mode, hash choice, precompile baseline, fork ladder, proof cadence, DA, transport, follower modes, etc.).
- `Nethereum.AppChain` is CoreChain + operator-configured knobs + sequencer / anchor / P2P / proof infrastructure. Ships three templates as presets: `AppChain-Financial`, `AppChain-Data`, `AppChain-Private`.
- `Nethereum.DevChain` is CoreChain with the `Ethereum` preset pinned — local dev against today's mainnet. Always Patricia + Keccak + RLP.

The seven sub-projects are **coordinated by this umbrella**; each has its own spec with brainstorm → design → implementation plan → commit cycle.

## The seven sub-projects

| # | Sub-project | Scope (one line) | Spec file |
|---|---|---|---|
| **A** | **Chain config surface** | All per-chain knobs defined at CoreChain level, three encoding modes, hardfork ladder, template scaffolds | `2026-04-20-appchain-config-surface-A.md` |
| **C** | **Proof generation (Zisk)** | Per-block (or periodic / on-demand) Zisk proof pipeline: witness → prover → artifact, with storage, retention, parallelism, HA | `2026-04-20-appchain-proof-generation-C.md` |
| **E** | **Data publishing + sync + verify** | libp2p swarm + content-addressed retrieval, published topics, follower modes, sync strategies, sequencer signing, DA layer choice | `2026-04-20-appchain-data-publishing-E.md` |
| **G** | **Storage backends + manifests** | Pluggable `IStorageBackend` adapters — AWS S3, Usenet/NZB+par2, BitTorrent, IPFS, Arweave — surfaced behind content-addressed retrieval. Manifest format. Data partitioning / erasure recovery | `2026-04-20-appchain-storage-backends-G.md` |
| **D** | **Proof aggregation** | Circom aggregator circuit + on-chain verifier contract compilation. Batches *N* per-block Zisk proofs into one cheap L1 verification | `2026-04-20-appchain-proof-aggregation-D.md` |
| **B** | **Anchoring protocol** | `AppChainAnchor` contract redesign, multi-chain targeting, anchor payload schema (state root + DA commitment + aggregated proof), finality gating for cross-chain | `2026-04-20-appchain-anchoring-B.md` |
| **F** | **Cross-chain messaging** | L1↔AppChain message passing, `AppChainHub` redesign, bridge surface consumed by AppChain-native AA and by L1 dApps | `2026-04-20-appchain-cross-chain-messaging-F.md` |

## Dependency graph

```
A  (chain config surface — foundation)
├── C  (proof generation)
│   └── D  (proof aggregation + on-chain verifier)
├── E  (data publishing + sync + verify)
│   └── G  (storage backends behind E's content-addressed retrieval)
└── (A is also direct foundation for B / F)

──── architecturally done ────

B  (anchoring — consumes A.state_root, C.proofs, D.aggregated_proof, E.DA_commitment, G.manifest_hash)
F  (cross-chain messaging — consumes A, B)
```

## Build order

1. **A** — define the full config surface at CoreChain level; wire three encoding modes; ship three AppChain templates + DevChain preset.
2. **C + E (parallel)** — proof pipeline + data publishing / sync / verify. Independent work streams.
3. **G** — layers on top of E's content-addressed retrieval; implements the storage backend adapters.
4. **D** — aggregator circuit + on-chain verifier; depends on C's artifact format.
5. **B** — anchoring builds on A (state root), C/D (proofs), E (DA commitment), G (manifest hash).
6. **F** — cross-chain messaging on top of B.

Each sub-project: brainstorm → spec → implementation plan → commits. Ship A's implementation before starting B in earnest.

## Cross-cutting concerns

Every sub-project spec has sections for:

- **HA / failure modes** — "SOLID" is the through-line. Each component names its failure domain and recovery procedure.
- **Observability / metrics** — what each subsystem exposes (proverLagBlocks, anchor-tx-health, peer-swarm-size, DA-backend-health, etc.).
- **Upgrade path** — how a hardfork flows through the subsystem.
- **Security boundaries** — what is trusted, what is cryptographically verified, where the boundaries are.
- **Scenario matrix** — for each config knob the sub-project owns, which values fit which AppChain class (Financial / Data / Private / Dev). "Document every scenario" is an explicit deliverable.

## Chain types and templates

| Chain type | Mode | Hash | Precompiles | `ProofGeneration` | `DataAvailability` | `P2PTransport` | `FollowerMode` default |
|---|---|---|---|---|---|---|---|
| **DevChain** | `Ethereum` | Keccak | Osaka | `Off` | `None` | `PullOnly` | `FullNode` |
| **AppChain-Private** | `Ethereum` or `EthereumBinaryV1` | Keccak or Poseidon | Osaka | `OnDemand` | `None` or `Committee` | `PullOnly` | `TrustedReplica` |
| **AppChain-Data** (games, business) | `EthereumBinaryV1` | Poseidon | Osaka | `Periodic(1000)` or `OnDemand` | `Committee` or `Calldata` | `Hybrid` | `FullNode` |
| **AppChain-Financial** | `EthereumBinaryV1` or `RoadmapSszV1` | Poseidon | Osaka | `Continuous` | `Blobs` (to L1) | `Hybrid` | `FullNode` |

Any combination is reachable — these are just the opinionated templates. The config surface (A) exposes every knob so operators can tune.

## Superseded planning documents

The following working documents at the repository root predate this umbrella and are **historical reference only** — architecture proposals there have been replaced or significantly reshaped by the specs in this series:

- `APPCHAIN_VISION.md` — naming rationale still useful; architecture superseded.
- `AppChain_ZK_Roadmap.md` — proposed Halo2 / PSE zkEVM; Zisk replaces.
- `APPCHAIN_E2E_USE_CASES.md` — use cases map into sub-project F.
- `APPCHAIN_HA_ROADMAP.md` — folds into cross-cutting HA sections of every sub-project.
- `APPCHAIN_PRODUCTION_ASSESSMENT.md` — operational concerns distributed across B/C/E.
- `APPCHAIN_ACCOUNT_ABSTRACTION_DESIGN.md`, `AAandBundlerWithPredeploymentsForAppChain.md` — AA design lives in F + existing `Nethereum.AccountAbstraction.AppChain`.
- `AppChainArchitectureReview.md` — preliminary; this umbrella replaces.

None are deleted; they stay in-tree as archaeology. The specs here are the canonical design.

## Status

- [x] Umbrella written.
- [x] A spec written in detail.
- [x] C spec written in detail.
- [x] E spec written in detail.
- [x] G scope + decisions captured (deep brainstorm pending).
- [x] D scope + decisions captured (deep brainstorm pending).
- [x] B scope + decisions captured (deferred).
- [x] F scope + decisions captured (deferred).
- [ ] Implementation plans (per sub-project, via `superpowers:writing-plans` skill) — after user review.

Nothing in this series is committed to git — the user's preference is to iterate freely in-workspace and commit only when ready to publish.
