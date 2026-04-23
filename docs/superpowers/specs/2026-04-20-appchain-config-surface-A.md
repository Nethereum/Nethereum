# Sub-project A — Chain Config Surface

**Date:** 2026-04-20
**Umbrella:** `2026-04-20-appchain-integration-umbrella.md`
**Status:** Design locked. Implementation plan to follow.

---

## Purpose

Every later sub-project commits to something computed over values the config surface produces — state root, block hash, proof, anchor payload, DA commitment. This sub-project defines the **full per-chain configuration surface at the `Nethereum.CoreChain` level**, which `DevChain` and `AppChain` both consume as presets.

It is NOT "AppChain state + block format" — that's a subset. The config surface covers encoding mode, hash function, precompile baseline, fork ladder, proof-generation policy, data availability, P2P transport, follower modes, witness retention, and more. All pluggable, all per-chain, all versioned through a hardfork ladder.

## Position

- `Nethereum.CoreChain` — owns the config types, defaults, presets, fork resolver.
- `Nethereum.DevChain` — pins `Ethereum` preset; local dev always matches current mainnet.
- `Nethereum.AppChain` — exposes three templates (`Financial`, `Data`, `Private`) that bundle a curated config; operators can override any knob.

AppChains are **accelerators of Ethereum's roadmap**. We implement draft EIPs early, give operators fast proof times, let the community run production-grade previews. Each AppChain pins its format at genesis and rides its own hardfork ladder forward; EIP drift is absorbed by new versioned forks, not breaking changes.

## Three encoding modes

Each AppChain picks one at genesis. All three shipped as first-class templates with educational READMEs — the three templates together double as a teaching tool for Ethereum's SSZ + binary-trie roadmap.

| Mode | Block header + body | State trie | Wire hash | Mainnet-wire-compatible? | Prove cost |
|---|---|---|---|---|---|
| **`Ethereum`** | RLP | Patricia + Keccak | Keccak | Yes, identical to today | Highest |
| **`EthereumBinaryV1`** | RLP | Binary + Poseidon (default) | Keccak on wire; Poseidon for state | Yes — only `stateRoot` derivation differs | Medium |
| **`RoadmapSszV1`** | SSZ (EIP-7807 stack) | Binary + Poseidon | SHA256 block hash, SSZ bodies | No — full divergence | Lowest |

### `Ethereum` — today's mainnet verbatim

Patricia trie, Keccak, RLP block + bodies. Identical to Ethereum. Picked by DevChain always and by AppChain operators who want zero tooling friction and don't care about prove time.

### `EthereumBinaryV1` — roadmap state, today's wire

Everything on the wire stays RLP and mainnet-identical: block header schema, transaction encoding, receipt encoding, withdrawal encoding. The only thing that changes is how `stateRoot` is computed — a binary trie per EIP-7864, hashed with Poseidon (default), or Keccak / SHA256 / Blake3 (selectable). `transactionsRoot` / `receiptsRoot` remain Patricia-of-RLP.

Block explorers, wallets, RPC clients, signers, and third-party analytics all see an Ethereum-shaped chain. The only software that breaks is anything reconstructing state from historical Patricia proofs — a small audience. Proving cost drops substantially because Poseidon is zkVM-cheap compared to Keccak.

**Default template** for new AppChains: operators get the Zisk proof speedup silently, without any tooling tax.

### `RoadmapSszV1` — full EIP-7807 stack

SSZ block header (EIP-7807), SSZ transactions (EIP-6404), SSZ receipts + log types (EIP-6466), SSZ withdrawals (EIP-6465), stable containers (EIP-7495), system logs (EIP-7799). Block hash is SHA256 per EIP-7807. State is binary trie + Poseidon per EIP-7864. No mainnet wire compat; this is the "where Ethereum is going" preview.

Picked when the operator wants maximum proving performance and accepts needing SSZ-aware tooling (which Nethereum ships alongside — explorer, SDK, signer updates follow).

## EIP set in scope

Only these EIPs are in scope for A. Anything else is deferred.

| EIP | Purpose | Used in |
|---|---|---|
| **EIP-7864** | Binary state trie, 256-slot stems, account data layout | `EthereumBinaryV1`, `RoadmapSszV1` |
| **EIP-6404** | SSZ typed transactions | `RoadmapSszV1` only |
| **EIP-6465** | SSZ withdrawals | `RoadmapSszV1` only |
| **EIP-6466** | SSZ typed receipts + log types | `RoadmapSszV1` only |
| **EIP-7495** | Stable containers (forward compat for SSZ schemas) | `RoadmapSszV1` only |
| **EIP-7799** | System logs (per-block balance-changing events) | `RoadmapSszV1` only |
| **EIP-7807** | SSZ execution block header, SHA256 block hash | `RoadmapSszV1` only |

Explicitly **out**: EIP-6493 (SSZ tx signature, fluid), EIP-7594 PeerDAS (belongs to G), Verkle / EIP-6800 (superseded by binary trie), EIP-7688 (consensus-layer scope).

## Account / trie layout

All `EthereumBinaryV1` and `RoadmapSszV1` AppChains adopt **EIP-7864 verbatim** — no AppChain-specific leaf additions. AppChain-specific metadata (cross-chain state, anchoring counters, sequencer metadata) lives in dedicated contracts, not in the account leaf schema.

```
Binary trie stem = hash(address ‖ sub_index_shift)[:31]   where hash = configured hash function

Stem layout (256 slots per stem):
  slot 0     — Basic Data Leaf: pack(version, code_size, nonce, balance)
  slot 1     — Code Hash
  slots 64+  — Code chunks (31-byte chunks, one per slot)
  slots 256+ — Storage (key derived per EIP-7864 from storage slot)
```

Already implemented in `Nethereum.Merkle.Binary/Keys/BinaryTreeKeyDerivation.cs` + `BasicDataLeaf.cs` + `CodeChunker.cs`. A's delta is wiring it into the fork config — no new leaf types.

## Transaction type support

All modes support every currently-live Ethereum transaction type: Legacy (type 0), EIP-2930 (type 1), EIP-1559 (type 2), EIP-4844 blob (type 3), EIP-7702 set-code (type 4). AppChains consume blobs from the parent chain; they never produce.

- `Ethereum` / `EthereumBinaryV1`: each type uses its existing RLP schema.
- `RoadmapSszV1`: each type is wrapped in its EIP-6404 SSZ container.

## Precompile baseline

All three modes start with the **Osaka** precompile set as the baseline — already wired in the Zisk build (ECRECOVER, SHA256, RIPEMD160, IDENTITY, MODEXP, BN128 add/mul/pairing, BLAKE2F, KZG point evaluation, BLS12-381 0x0b..0x11, P256VERIFY at 0x100). An operator can pick an earlier fork's precompile set on the AppChain hardfork ladder; default is Osaka.

## Fork ladder / config surface

```csharp
namespace Nethereum.AppChain
{
    public enum AppChainFork
    {
        // Ethereum mode
        Ethereum,                // = Osaka mainnet

        // EthereumBinary mode (versioned for EIP drift)
        EthereumBinaryV1,        // EIP-7864 draft pinned

        // RoadmapSsz mode (versioned for EIP drift)
        RoadmapSszV1             // EIP-7807 + deps draft pinned
    }
}
```

Versioned suffix (`V1`, `V2`) means EIP draft bumps get picked up by a new fork on the ladder, without breaking AppChains pinned on `V1`. Migration between versions uses the normal fork-activation mechanism (timestamp or block number).

## Complete configuration surface

Every knob defined by A. All are per-chain, set at genesis, override-able via hardfork.

```csharp
public class ChainConfig
{
    // ── A: Format and execution
    public AppChainFork Fork                       { get; set; }  // = Ethereum / EthereumBinaryV1 / RoadmapSszV1
    public HashFunction StateTrieHash              { get; set; }  // Poseidon / Keccak / SHA256 / Blake3
    public HardforkName PrecompileBaseline         { get; set; }  // = Osaka by default
    public TxTypeSet TransactionTypes              { get; set; }  // subset or all of legacy/2930/1559/4844/7702
    public int MaxCodeSize                         { get; set; }  // EIP-170 default 24576
    public int MaxInitcodeSize                     { get; set; }  // EIP-3860 default 49152
    public long GasLimit                           { get; set; }
    public long BlockTimeMs                        { get; set; }

    // ── C: Proof generation
    public ProofGenerationMode ProofGeneration     { get; set; }  // Off / OnDemand / Periodic(N) / Continuous
    public WitnessRetentionPolicy WitnessRetention { get; set; }  // UntilProven / Days(N) / Blocks(N) / Forever
    public StorageBackendChoice ProofStorage       { get; set; }  // Postgres / Filesystem / custom via IProofStorage

    // ── E: Data publishing + sync + verify
    public DataAvailabilityMode DataAvailability   { get; set; }  // None / Committee(N,K) / Calldata / Blobs / custom
    public P2PTransportMode P2PTransport           { get; set; }  // PullOnly / Hybrid
    public PublishTopicSet PublishTopics           { get; set; }  // which topics are gossiped vs fetch-only
    public FollowerMode FollowerModeDefault        { get; set; }  // FullNode / LightClient / TrustedReplica
    public SyncStrategy SyncStrategyDefault        { get; set; }  // FullSync / SnapSync / LightSync / LiveOnly
    public SequencerSignatureScheme SequencerSig   { get; set; }  // Secp256k1 / Bls / Ed25519
    public SequencerRegistryMode SequencerRegistry { get; set; }  // GenesisPinned / OnChainRegistry

    // ── B: Anchoring (deferred; config surface sketched for future wiring)
    public AnchoringConfig Anchoring               { get; set; }  // target chain(s), cadence, payload shape

    // ── F: Cross-chain messaging (deferred)
    public CrossChainMessagingConfig Messaging     { get; set; }
}
```

Every value is validated at genesis by a `ChainConfigValidator`. Cross-knob rules (e.g., `ProofGeneration = OnDemand` forbids `WitnessRetention = UntilProven`) are enforced there.

## Composition with `HardforkConfig`

`AppChainHardforkConfigs.GetConfig(AppChainFork, hashProvider)` composes existing building blocks:

- `OpcodeHandlerSets.Osaka`
- `IntrinsicGasRuleSets.Osaka`
- `PrecompileRegistries.OsakaBase` (wired via appropriate backend set — Zisk or Herumi+CKZG)
- `CallFrameInitRuleSets.Osaka`
- `TransactionValidationRuleSets.Osaka` / `TransactionSetupRuleSets.Osaka`
- `BinaryStateRootCalculator(hashProvider)` or `PatriciaStateRootCalculator` for `Ethereum` mode
- For `RoadmapSszV1`: `SszBlockEncodingProvider` (new) in place of `RlpBlockEncodingProvider`

Most of the composition already exists. A's delta is: (a) the enum + factory, (b) the SSZ block encoding provider, (c) the three template scaffolds, (d) the `ChainConfig` type + validator.

## Templates

`Nethereum.AppChain.Templates` ships three templates. Each is a minimal runnable AppChain node + genesis file + README.

```
templates/
├── appchain-ethereum/
│   ├── README.md           — "Today's mainnet, portable"
│   ├── genesis.json        — fork: Ethereum
│   └── Program.cs
├── appchain-ethereum-binary/
│   ├── README.md           — "Mainnet wire, binary-trie state, prove-fast"
│   ├── genesis.json        — fork: EthereumBinaryV1, hash: Poseidon
│   └── Program.cs
└── appchain-roadmap-ssz/
    ├── README.md           — "Ethereum's roadmap, shipped today"
    ├── genesis.json        — fork: RoadmapSszV1
    └── Program.cs
```

Each README covers:

1. What the mode is in one paragraph.
2. A table of what's encoded how (block header, tx, receipt, state trie, hash).
3. Which EIPs it implements and their upstream draft status.
4. What tools work as-is vs. what needs AppChain-aware tooling.
5. Expected proving cost relative to the other two modes.
6. When to pick this mode vs. the others.

Scaffolding via `Nethereum.AppChain.Templates.New --mode=<Ethereum|EthereumBinary|RoadmapSsz>`.

## Cross-cutting concerns

### HA / failure modes
- Config validation at genesis is non-recoverable: invalid genesis = refusal to boot.
- Hardfork upgrade: multi-stage (announce → schedule → activate) to avoid split-network issues. Followers that haven't upgraded past the fork boundary halt cleanly.
- Operator-supplied hash / crypto backend failure: node refuses to start with a non-functioning backend (no silent fallback).

### Observability
- Every `ChainConfig` serialises to a canonical JSON exposed at `/debug/config` on the node for operator inspection.
- Metric `chain.fork.current` surfaces the active fork name.
- Startup log prints the full resolved config.

### Upgrade path
- Fork ladder: `EthereumBinaryV1` → `EthereumBinaryV2` is a real hardfork (transition block / timestamp) with clear migration rules. State trie migrations (if any) happen at the fork boundary.
- Mode-changing forks (`EthereumBinaryV1` → `RoadmapSszV1`) are possible but require one-shot state re-computation + body re-encoding. Design note: this is a significant operation, operators should expect downtime for the boundary block's reorg protection window.

### Security boundaries
- Genesis config is **trusted** — signed by the AppChain operator, distributed with the node binary or fetched from a pinned URL.
- Per-fork rule changes are **on-chain** — the `SequencerRegistry` contract (from E) stores the active fork + scheduled fork changes; followers accept fork transitions only if committed on-chain.

### Scenario matrix

| Class | `Fork` | `StateTrieHash` | `ProofGeneration` | `DataAvailability` | `P2PTransport` | `FollowerMode` default |
|---|---|---|---|---|---|---|
| DevChain | `Ethereum` | `Keccak` | `Off` | `None` | `PullOnly` | `FullNode` |
| AppChain-Private | `Ethereum` / `EthereumBinaryV1` | `Keccak` / `Poseidon` | `OnDemand` | `None` / `Committee` | `PullOnly` | `TrustedReplica` |
| AppChain-Data | `EthereumBinaryV1` | `Poseidon` | `Periodic(1000)` / `OnDemand` | `Committee` / `Calldata` | `Hybrid` | `FullNode` |
| AppChain-Financial | `EthereumBinaryV1` / `RoadmapSszV1` | `Poseidon` | `Continuous` | `Blobs` | `Hybrid` | `FullNode` |

## Implementation Status (Updated 2026-04-23)

| Component | Status | Notes |
|---|---|---|
| Binary trie (EIP-7864) | **DONE** | BinaryTrie, StemBinaryNode, CachedValuesMerkleizer, spec-validated |
| Poseidon BN254 Montgomery | **DONE** | BN254FieldElement, BN254PoseidonCore, ~1.6x Patricia |
| Pluggable interfaces | **DONE** | IBlockHashProvider, IBlockEncodingProvider, IBlockRootsProvider, IAccountLayoutStrategy, IProofService |
| RLP implementations | **DONE** | RlpBlockEncodingProvider, RlpKeccakBlockHashProvider, RlpAccountLayout |
| SSZ implementations | **DONE** | SszBlockEncodingProvider, SszBlockRootsProvider, SszSha256BlockHashProvider |
| EIP-4844 blob transactions | **DONE** | Transaction4844, BlobEncoder, KZG pipeline, high-level API, ~49 tests |
| SSZ hash_tree_root blob tx (0x09) | **DONE** | HashTreeRootTransaction4844, encode/decode, 5 tests |
| DevChain blob storage | **DONE** | IBlobStore, InMemoryBlobStore, beacon REST endpoint |
| Beacon blob client | **DONE** | BeaconApiClient.GetBlobSidecarsAsync, BlobSidecarHelper |
| `AppChainFork` enum | **EXISTS** | `src/Nethereum.AppChain/AppChainFork.cs` — needs expansion |
| `ChainConfig.StateTree` / `StateTreeHashProvider` | **EXISTS** | In ChainConfig, used by DevChain |
| `SszBlockEncodingProvider` | **DONE** | Full encode/decode for headers, txs (1559/4844/7702), receipts, withdrawals |
| `ChainConfig` full surface | **TODO** | Needs all knobs from spec above |
| `ChainConfigValidator` | **TODO** | Cross-knob rule enforcement |
| `AppChainHardforkConfigs` factory | **TODO** | `AppChainFork → HardforkConfig` composition |
| Three template scaffolds | **TODO** | AppChain-Ethereum, AppChain-EthereumBinary, AppChain-RoadmapSsz |
| CLI scaffolder | **TODO** | `Nethereum.AppChain.Templates.New` |

## Critical files to modify

| File / path | Change | Status |
|---|---|---|
| `src/Nethereum.CoreChain/ChainConfig.cs` | Expand with full knob surface | PARTIAL — has StateTree/StateTreeHashProvider |
| `src/Nethereum.CoreChain/ChainConfigValidator.cs` | New — cross-knob rule enforcement | TODO |
| `src/Nethereum.AppChain/AppChainFork.cs` | Expand enum | PARTIAL |
| `src/Nethereum.AppChain/AppChainHardforkConfigs.cs` | New — factory `AppChainFork → HardforkConfig` | TODO |
| `src/Nethereum.EVM.Core/Witness/BlockFeatureConfig.cs` | Confirm coverage for all three modes | CHECK |
| `src/Nethereum.Model.SSZ/SszBlockEncodingProvider.cs` | SSZ block encoding — **DONE** | DONE |
| `src/Nethereum.AppChain.Templates/` | Three template scaffolds + three READMEs | TODO |
| `src/Nethereum.AppChain.Templates/New.cs` | CLI scaffolder | TODO |

## Non-goals for A

- Anchoring contract schema — sub-project B.
- Proof pipeline operational shape — sub-project C.
- Data publishing — sub-project E.
- Storage backends — sub-project G.
- Cross-chain messaging — sub-project F.

## Verification

1. All three templates scaffold clean from CLI: `Nethereum.AppChain.Templates.New --mode=<X>` produces a building AppChain node.
2. Each template's node boots, produces at least 10 blocks under the chosen fork, and the reported `stateRoot` uses the expected calculator (Patricia+Keccak for `Ethereum`, Binary+Poseidon for `EthereumBinaryV1` / `RoadmapSszV1`).
3. `ziskemu` + existing EVM.Zisk ELF + a witness captured from each template's block execution reproduces the same `stateRoot` the template's node reported — closes managed ↔ zkVM parity loop for all three modes.
4. `ChainConfigValidator` rejects every invalid cross-knob combination with a human-readable message (unit-tested).
5. READMEs rendered + proofread: explain the mode, cite EIPs with upstream draft status, include a decision-tree for picking a mode.
