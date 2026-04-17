# Binary State Trie — Implementation Plan

## Spec Reference
- **EIP-7864**: Ethereum State Using a Unified Binary Tree
- **EIP-7748**: State migration (MPT → binary, future EIP)
- Cross-language vectors: jsign/binary-tree-spec, ethereumjs/binarytree, eth2030/pkg/trie/bintrie

## Hash Function Strategy

BLAKE3 is the EIP-7864 default. Poseidon provides ~10x prover efficiency.

Per Vitalik (ethereum-magicians EIP-7864 discussion):
- BLAKE3: beautiful, secure, good out-of-circuit performance
- Poseidon: decisive advantage for (1) hash-based signature aggregation and (2) separated proving — fast stateful tree-only prover locally, slower stateless outsourced prover. Enables any AI cluster to be repurposed for Ethereum proving in <60 sec without holding state.
- Direction: push for both — secure Poseidon + more efficient BLAKE3 proving (GKR or similar)

Our design: `IHashProvider` pluggable at `ChainConfig` level. BLAKE3 default, Poseidon via `PoseidonPairHashProvider`, SHA256 also available. All three validated in `BinaryStateRootCalculatorTests`.

## Storage Paging — How Contracts Map to Stems

Each account gets a stem at treeIndex=0 holding BasicData (nonce/balance/code_size), CodeHash, inline storage (slots 0-63), and first 128 code chunks — all in one proof.

Large contracts (e.g. USDC ~8-12M stems for balances + allowances) distribute mapping entries across unique stems (keccak-derived keys are uniformly spread). One stem per mapping entry, each with 1 populated value out of 256 slots.

Serving patterns this enables:
- **Account + hot storage**: 1 proof (stem 0, suffixes 0-127)
- **Single storage slot**: 1 proof (~992 bytes, 31 siblings)
- **Top-of-tree checkpoint**: top 20 levels = ~32 MB → any stem verifiable with 11 siblings (352 bytes)
- **Per-contract subtree**: download all stems for one address + siblings to root
- **Per-block diff**: only changed stems + sibling proofs → light clients apply incrementally

## What's Done

### EIP-7864 Primitives (Nethereum.Merkle.Binary)
- [x] BinaryTrie (stem + internal + empty + hashed nodes)
- [x] BinaryTreeKeyDerivation (address → stem, suffix mapping per EIP-6800)
- [x] BasicDataLeaf (32-byte packed: version + code_size + nonce + balance)
- [x] CodeChunker (31-byte chunks with PUSH continuation byte)
- [x] CompactBinaryNodeCodec (encode/decode)
- [x] ValuesMerkleizer (256-element sparse merkle)
- [x] BinaryTrieProver + BinaryTrieProofVerifier
- [x] IBinaryTrieStorage + InMemoryBinaryTrieStorage
- [x] Blake3HashProvider (managed, no native deps)
- [x] Spec vectors matching jsign + ethereumjs + eth2030

### Hash Providers (Nethereum.Util)
- [x] IHashProvider interface
- [x] PoseidonHasher (T1-T16 presets, BN254)
- [x] PoseidonPairHashProvider (optimized 2-input for trie internal nodes)
- [x] Sha3KeccackHashProvider
- [x] Sha256HashProvider

### State Root Calculators (CoreChain)
- [x] IStateRootCalculator interface (EVM.Core)
- [x] PatriciaStateRootCalculator (Keccak MPT, current Ethereum)
- [x] BinaryStateRootCalculator (EIP-7864 witness path, pluggable hash)
- [x] BinaryIncrementalStateRootCalculator (persistent IStateStore path, dirty tracking)
- [x] ChainConfig.CreateStateRootCalculator() factory + CreateBinaryIncrementalStateRootCalculator()
- [x] StateTreeType enum (Patricia / Binary)
- [x] Tests: 10 witness-path (empty/deterministic/sensitivity/BLAKE3/Poseidon/SHA256/Patricia-vs-Binary)
- [x] Tests: 7 incremental (cross-validated against witness-path calculator for BLAKE3/Poseidon/SHA256, incremental-vs-full-rebuild parity, dirty tracking)

## What's Next

### Phase 1c — Storage layer for checkpoints, proofs, and light clients

Enhance `IBinaryTrieStorage` from simple KV to support the three serving patterns:

**1. Node store (persistent backend)**
Trie nodes keyed by hash, with depth + stem-address metadata. Enables:
- Checkpoint export: all nodes at depth ≤ N
- Contract subtree extraction: all stem nodes for a given address
- Proof serving: walk from leaf to root returning siblings

Extend `IBinaryTrieStorage`:
- [ ] `GetNodesAtDepth(int maxDepth)` — checkpoint export
- [ ] `GetStemsByAddress(byte[] address)` — per-contract subtree
- [ ] `GetDirtyNodes()` / `ClearDirtyTracking()` — per-block diff tracking
- [ ] Depth + address indexing on Put
- [ ] RocksDB / SQLite persistent implementations

**2. Multi-proof generator**
- [ ] Given a set of keys, produce a single compact proof (shared siblings deduplicated)
- [ ] Extend `BinaryTrieProver` for batch proofs
- [ ] Verify batch proofs in `BinaryTrieProofVerifier`

**3. Checkpoint snapshot**
- [ ] Serialize top N levels of trie as a portable blob (32 MB for top 20 at ~2B stems)
- [ ] Import checkpoint — reconstruct top-of-tree from blob
- [ ] Incremental checkpoint update: apply `BlockStateDiff` to update top-of-tree

### Phase 1d — Wire into BlockProducer
- [ ] `BlockProducer` checks `ChainConfig.StateTree` and uses `BinaryIncrementalStateRootCalculator` when configured
- [ ] `BlockManager` / `DevChainNode` pass the calculator through

### Phase 1e — Witness integration
- [ ] `BinaryBlockWitness` v2 carrying `StateTreeType` in features
- [ ] Zisk guest able to use binary trie (source-link `BinaryStateRootCalculator` into EVM.Core build)

### Phase 2 — SSZ internal artifacts + BlockStateDiff

**BlockStateDiff** is the per-block publishable unit:
- Changed stem keys + new values (from `GetDirtyNodes()`)
- Sibling hashes needed to verify each change against pre-state root
- Pre-state root + post-state root
- SSZ-encoded for deterministic serialization

Types to build:
- [ ] `BlockStateDiff` SSZ type — the per-block state transition proof
- [ ] SSZ witness encoding (replacing BinaryBlockWitness v1 custom binary format)
- [ ] `BatchManifest` SSZ type — groups blocks into batches for anchoring
- [ ] `ZkPublicInputs` SSZ type — commitments for the ZK circuit

### Per-contract light client — the key use case

A wallet or DApp syncs only the contracts it cares about (e.g. USDC, Uniswap router, an L2 bridge) and nothing else. Fully verified against the global state root.

**Initial sync (USDC example):**
1. Request all stems for address = USDC → ~8-12M stems (~1.2 GB with proofs)
2. Or: request just the account stem (nonce/balance/code, 992 bytes) + specific holders on demand (992 bytes each)
3. Each proof verified against the published state root — trustless

**Ongoing sync:**
1. Subscribe to `BlockStateDiff` filtered by contract address(es)
2. Per block: receive only the stems that changed for the subscribed contracts (~few KB)
3. Verify each changed stem's proof against the pre-state root
4. Update local contract state
5. Sibling hashes from the diff keep the local proof chain valid against the global root

**What this enables:**
- A USDC wallet holds ~1.2 GB (full USDC state) or ~1 MB (account + 1000 holders) — not 300 GB of full Ethereum state
- Each balance query is locally resolved AND verified against the chain root
- Per-block updates are ~few KB (only USDC stems that changed in that block)
- No RPC trust dependency — the wallet IS a verified light client for its contracts
- Multiple contracts composable: subscribe to USDC + WETH + Uniswap → verified DeFi state in ~3 GB

**Storage layer requirement:** `GetStemsByAddress(address)` and `BlockStateDiff` filtering by stem derivation address make this possible. The key insight is that ALL stems for a given contract share the same address input to `GetTreeKey(address, treeIndex, subIndex)` — so filtering is deterministic.

### Full light client flow (for nodes that track the whole tree)
1. Hold a top-20-levels checkpoint (32 MB)
2. Receive `BlockStateDiff` per block (~350 KB for ~1000 changed stems)
3. Verify diff against pre-state root using sibling proofs
4. Update checkpoint with new node hashes
5. New local root matches published post-state root

### Phase 3 — Zisk v2 (separated proving)
- [ ] Poseidon precompile backend for Zisk guest (native via libziskos or managed)
- [ ] SSZ witness (`BlockStateDiff`) deserialization in guest
- [ ] Tree-only prover: takes `BlockStateDiff` + pre-state root → proves post-state root transition (fast, Poseidon-native, runs locally on block builder)
- [ ] Execution prover: takes block transactions + witness → proves EVM execution correctness (slower, outsourceable, doesn't need state)
- [ ] Proof composition: tree proof + execution proof → single verifiable proof

### Phase 4 — Anchoring + data publishing
- [ ] L1 anchor format: `BatchManifest` SSZ + state root commitment
- [ ] Blob encoding: `BlockStateDiff` batches packed into EIP-4844 blobs
- [ ] External DA pointers: for diffs exceeding blob capacity, pointer to Celestia/EigenDA/etc.
- [ ] Anchor contract: verify batch manifest hash + state root on L1

### Phase 5 — Pre-anchor gossip
- [ ] Gossip `BlockStateDiff` to peers as blocks are produced (before L1 anchoring)
- [ ] Peers verify diff against their local checkpoint
- [ ] Batch manifest assembly: collect diffs until anchor threshold
- [ ] P2P protocol: topic-based pubsub keyed on chain ID + batch sequence

## Realistic Sizing and Open Problems

### Proof sizes (honest math)

| Scenario | Stems touched | Diff size | Per-contract filtered |
|----------|---------------|-----------|----------------------|
| Simple transfer (2 accounts) | 2 | ~1.2 KB | ~1.2 KB |
| Realistic DeFi block (200 txs) | ~500 | ~200 KB | ~2-4 KB (per contract) |
| Heavy block (1000 txs, DEX arb) | ~2000 | ~800 KB | ~5-10 KB |
| Per day at 1 block/sec | | ~17 GB full | ~350 MB per contract |

The per-contract filtering makes wallet-side bandwidth manageable even at high throughput. Full validators pay the full cost.

### Gossip heterogeneity

Three client types with different needs → separate gossip topics, not one-size-fits-all:
- `chain/{id}/blocks/full` — full BlockStateDiff (validators, full nodes)
- `chain/{id}/blocks/roots` — just block number + roots (light clients pull diffs on demand)
- `chain/{id}/contract/{address}` — server-side filtered diffs (wallets subscribe per contract)

Open: relay nodes that serve the filtered topics need to hold full state or at minimum the address→stem index. This is a relay infrastructure cost.

### Bootstrap / cold-start problem

A new USDC wallet doesn't know which stems hold USDC data. Key derivation is deterministic (given the contract address, you CAN compute stems), but you don't know which treeIndexes have data without scanning.

Realistic cold-start paths:
1. **Address index RPC**: `eth_getContractStems(address)` → returns all stem hashes for that contract. Any full node can serve this. Our `IBinaryTrieNodeStore.GetStemNodesByAddress()` provides the backing data.
2. **Contract manifest**: the contract publisher registers its address when joining the network. Initial sync downloads all stems + proofs from any peer.
3. **Incremental discovery**: subscribe to the contract's gossip topic, accumulate diffs over time. Eventually converges to full contract state. Slower but requires no special sync protocol.

Open: none of these are trustless without verifying against a known state root. The wallet needs to either (a) have a trusted root from L1, or (b) verify a checkpoint proof.

### DA coordination (unsolved, operational)

18-day blob retention on L1 means diffs need external archiving for historical access. This is a hard coordination problem:
- AppChain with a known operator → operator archives, retention is an SLA
- Decentralised → needs economic incentives for redundant archiving (EigenDA, Celestia, etc.)
- The architecture provides the encoding and anchor format; the DA layer is pluggable but not free

Open: archiving economics, redundancy guarantees, archive discovery protocol.

### Economic sustainability (business model, not just technical)

| Cost | Who pays | How |
|------|----------|-----|
| Tree proving (Poseidon, per block) | Sequencer/block producer | Amortised into block production |
| EVM proving (per block) | Outsourced market | Prover marketplace, bid per block |
| L1 blob costs (per batch) | Batch submitter | Amortised over N blocks in the batch |
| Archive storage | Operator or protocol treasury | Per-GB pricing, long-term commitment |
| Relay infrastructure | Relay operators | Fee market or altruistic / protocol-subsidised |

Open: pricing models, who-subsidises-what during bootstrap, prover marketplace design.

## Architecture Summary

```
Block Producer
    │
    ├─ BinaryIncrementalStateRootCalculator (computes root from dirty stems)
    │       │
    │       └─ IBinaryTrieStorage (persistent, depth + address indexing)
    │               │
    │               ├─ GetDirtyNodes() ──────────────────────┐
    │               ├─ GetNodesAtDepth(20) ──────┐           │
    │               └─ GetStemsByAddress(addr) ──│───────────│──► Per-contract subtree
    │                                            │           │
    │                                            │           │
    ▼                                            ▼           ▼
BlockStateDiff (SSZ)                     Full Light Client   Per-Contract Client
    │                                    (32 MB checkpoint)  (USDC wallet: ~1 MB)
    │                                         │                    │
    ├─ Gossip to peers (Phase 5)              │                    │
    ├─ Batch into BatchManifest (Phase 4)     │                    │
    └─ Anchor to L1 / DA (Phase 4)           │                    │
                                              ▼                    ▼
                                    Apply full diff          Apply filtered diff
                                    (~200 KB/block           (~2-4 KB/block per
                                     realistic DeFi)          subscribed contract)
                                              │                    │
                                              └───── both ─────────┘
                                                      │
                                                Verify against
                                              published state root

Tree-only Prover (Poseidon, local)            Execution Prover (outsourced)
    │                                              │
    └─ Proves state root transition ◄─────────────┘ Proves EVM correctness
                    │
                    ▼
            Composed Proof (verifiable by anyone)
```
