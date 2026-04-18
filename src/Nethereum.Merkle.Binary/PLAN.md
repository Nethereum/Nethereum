# Binary State Trie + State Diffs — Implementation Plan

## Standards Landscape (researched 2026-04-17)

### EIP-7864 (Binary Trie) — Draft, Active
Defines the tree structure: 31-byte stem + 1-byte suffix, 256-wide stem nodes, unified (accounts + storage in one tree). Does NOT define a state diff, witness, or proof format. Hash function TBD (BLAKE3 provisional; Poseidon2, Keccak under evaluation). **Our tree implementation is aligned.** Cross-language test vectors match jsign/binary-tree-spec, ethereumjs/binarytree, eth2030/pkg/trie/bintrie.

### Verkle ExecutionWitness (consensus-specs) — Closest Prior Art for Diffs
Defines `StemStateDiff { stem: Bytes31, suffix_diffs: List[SuffixStateDiff] }` with `SuffixStateDiff { suffix: uint8, current_value: Optional[Bytes32], new_value: Optional[Bytes32] }`. Our `StemDiff`/`SuffixDiff` mirrors this structure. Key difference: Verkle uses IPA/Bandersnatch commitments; binary trie uses hash-based Merkle siblings. EIP-7612 (Verkle overlay transition) is STAGNANT; binary trie (EIP-7864) is the active direction.

### EIP-7928 (Block-Level Access Lists / BALs) — CONFIRMED for Glamsterdam (H1 2026)
Per-transaction account/storage changes (address → balance/nonce/code/slots), RLP-encoded, embedded in blocks. Operates at the **account/storage layer** (WHAT changed) — complementary to our trie-level diffs (HOW the tree changed). **Future: consume BALs → produce trie diffs without re-execution.**

### EIP-6404/6466/7495/7807 (SSZ Execution Layer) — All Draft
Eliminate MPT from block commitments except state trie. Wire format moving to SSZ. No proposal for SSZ as internal storage format — disk encoding is client-specific. **Our compact binary encoding is fine internally; add SSZ containers for wire interop when needed.**

### EIP-7862 (Delayed State Root) — Draft
Block N stores state root of block N-1. Our `PreStateRoot`/`PostStateRoot` design is directly compatible.

### No Standard Binary Trie State Diff Format Exists
We are defining `BinaryTrieStateDiff` v1. Risk: a standard may emerge. Mitigation: our structure mirrors the Verkle pattern, format is versioned, encoding is internal.

### Portal Network — Different Model
Individual key-value lookups with MPT proofs, not batched diffs. Not relevant to our architecture.

## Hash Function Strategy

Per Vitalik (ethereum-magicians EIP-7864 discussion):
- **BLAKE3**: secure, good out-of-circuit, EIP-7864 provisional default
- **Poseidon**: ~10x prover efficiency, decisive for (1) hash-based signature aggregation and (2) separated proving — fast stateful tree-only prover locally, slower stateless outsourced prover. Enables any machine to be repurposed for Ethereum proving in <60 sec without holding state.
- **Direction**: push for both — secure Poseidon + more efficient BLAKE3 proving (GKR or similar)

Our design: `IHashProvider` pluggable at `ChainConfig` level. BLAKE3 default, Poseidon via `PoseidonPairHashProvider`, SHA256 also available. All validated in tests.

## Storage Paging — How Contracts Map to Stems

Each account gets a stem at treeIndex=0: BasicData (nonce/balance/code_size) at suffix 0, CodeHash at suffix 1, inline storage (slots 0-63) at suffixes 64-127, first 128 code chunks at suffixes 128-255.

Large contracts (e.g. USDC ~8-12M stems for balance + allowance mappings): each keccak-derived mapping key lands in a unique stem. One stem per mapping entry, 1 populated value out of 256 slots.

Serving patterns:
- **Account + hot storage**: 1 proof (stem 0, all 256 suffixes)
- **Single storage slot**: 1 proof (~992 bytes, 31 siblings at depth 31)
- **Top-of-tree checkpoint**: top 20 levels = ~32 MB → any stem verifiable with 11 siblings
- **Per-contract subtree**: all stems for one address (via `RegisterAddressStem` index)
- **Per-block diff**: only changed stems + proof siblings → light clients apply incrementally

## What's Done

### Phase 1a — EIP-7864 Primitives (Nethereum.Merkle.Binary) ✅
- BinaryTrie (stem + internal + empty + hashed nodes)
- BinaryTreeKeyDerivation (address → stem, suffix mapping)
- BasicDataLeaf (32-byte packed: version + code_size + nonce + balance)
- CodeChunker (31-byte chunks with PUSH continuation)
- CompactBinaryNodeCodec, ValuesMerkleizer, Blake3HashProvider
- BinaryTrieProver + BinaryTrieProofVerifier
- IBinaryTrieStorage + InMemoryBinaryTrieStorage
- Spec vectors matching jsign + ethereumjs + eth2030

### Phase 1a — Hash Providers (Nethereum.Util) ✅
- IHashProvider interface
- PoseidonHasher (T1-T16 presets, BN254), PoseidonPairHashProvider
- Sha3KeccackHashProvider, Sha256HashProvider

### Phase 1b — State Root Calculators (CoreChain) ✅
- BinaryStateRootCalculator (EIP-7864 witness path, pluggable hash) — 10 tests
- BinaryIncrementalStateRootCalculator (persistent IStateStore path, dirty tracking) — 7 tests cross-validated against witness path
- IIncrementalStateRootCalculator interface (Patricia + Binary both implement)
- ChainConfig: StateTreeType enum, CreateStateRootCalculator() factory
- BlockProducer / BlockManager / AppChain.Sequencer wired via IIncrementalStateRootCalculator

### Phase 1c — Node Store for Checkpoints + Light Clients (Merkle.Binary) ✅
- IBinaryTrieNodeStore: PutNode (depth/type/stem metadata), RegisterAddressStem (address→stem index)
- GetNodesByDepthRange (checkpoint export), GetStemNodesByAddress (per-contract subtree)
- GetDirtyNodes / ClearDirtyTracking / MarkBlockCommitted (per-block diff tracking)
- ExportCheckpoint / ImportCheckpoint (portable top-N-levels blob)
- InMemoryBinaryTrieNodeStore implementation — 8 tests

### Phase 2a — State Diff Types (Merkle.Binary.StateDiff) ✅
- BinaryTrieStateDiff v1 (version byte, block number, pre/post state roots, stem diffs, proof siblings)
- StemDiff / SuffixDiff (nullable OldValue/NewValue matching Verkle Optional pattern)
- BinaryTrieStateDiffEncoder (flags-based encoding, skips null values)
- BinaryTrieStateDiffProducer (from IBinaryTrieNodeStore dirty tracking) — 5 tests

### Phase 2b — Batch Integration (AppChain.Sync) ✅
- BatchInfo extended: FromBlockStateRoot, DiffHashes, TotalDiffBytes, ContentType
- BatchContentType enum: FullBlocks (existing) | StateDiffs (new)

## What's Next

### Phase 2c — StateDiffBatchWriter / Reader (AppChain.Sync)
- [ ] `StateDiffBatchWriter : IBatchWriter` — serialises BinaryTrieStateDiff entries into batch files
- [ ] `StateDiffBatchReader : IBatchReader` — deserialises state diff batches
- [ ] Round-trip tests (write N diffs → read back → all fields match)

### Phase 2d — Sequencer Wiring (AppChain.Sequencer)
- [ ] `SequencerBatchProducer` accumulates `BinaryTrieStateDiff` per block when `StateTree == Binary`
- [ ] Produces state diff batches via `StateDiffBatchWriter` at cadence/threshold
- [ ] `BatchInfo.ContentType = StateDiffs` + populated DiffHashes

### Phase 2e — Witness Integration
- [ ] `BinaryBlockWitness` v2 carrying `StateTreeType` in features
- [ ] Zisk guest able to use binary trie (source-link `BinaryStateRootCalculator` into EVM.Core build)

### Phase 2f — Multi-proof Generator
- [ ] Given N keys, produce a single compact proof (shared siblings deduplicated)
- [ ] Extend `BinaryTrieProver` for batch proofs
- [ ] Diff verification: apply `BinaryTrieStateDiff` + verify post-state root

### Phase 3 — Zisk Native Crypto + Separated Proving

**Native Poseidon2 in Zisk guest — BLOCKER IDENTIFIED (2026-04-18):**

`zkvm_poseidon2` (CSR 0x812) exists in `zisk_syscalls.S` and is linked in `libziskos.a`.
Adding `[DllImport("__Internal")] extern void zkvm_poseidon2(ulong* state)` causes the
NativeAOT linker to extract `zisk_syscalls.o` from the archive, which includes
`zkvm_dma_memcpy` (CSR 0x813). The .NET runtime's `memcpy` is `--wrap`'d to route
through `zkvm_dma_memcpy`, but the instruction sequence (`csrs; ret`) doesn't match
what the Zisk transpiler expects (`csrs; addi` for DMA protocol). This breaks ALL
witness types, not just Poseidon.

Fix options:
- **Option A**: Write our own `zisk_syscalls.S` WITHOUT `zkvm_dma_memcpy`/`zkvm_dma_memcmp`
  (omit CSR 0x813/0x814). Linking `zkvm_poseidon2` won't pull in DMA symbols.
- **Option B**: Inline RISC-V assembly in C# (`[MethodImpl(MethodImplOptions.InternalCall)]`
  or custom ILC intrinsic) to emit `csrs 0x812, a0` directly — bypasses DllImport entirely.
- **Option C**: Patch bflat's `--wrap memcpy` to not route through `zkvm_dma_memcpy`.

Current state:
- [x] `ZiskPoseidonHashProvider` exists as stub (throws NotSupportedException)
- [x] Managed Poseidon (`PoseidonEvmHasher`) works in .NET tests, NOT in Zisk guest
  (large static array init triggers DMA memcpy + NativeAOT devirt issues)
- [x] Blake3 binary trie works in Zisk guest (BIN:OK)
- [x] `ZiskCrypto.cs` has raw syscall P/Invokes documented but disabled
- [ ] Resolve DMA memcpy linker issue (Option A recommended — simplest)
- [ ] Wire native `zkvm_poseidon2` into `ZiskPoseidonHashProvider`
- [ ] Also wire `zkvm_secp256r1_add`/`_dbl` for P256Verify (Osaka)
- [ ] Consider native keccak/sha256 via `zkvm_keccakf`/`zkvm_sha256f` for perf

Separated proving (after native crypto works):
- [ ] Tree-only prover: proves state root transition (Poseidon, fast, local)
- [ ] Execution prover: proves EVM correctness (outsourceable, stateless)
- [ ] Proof composition: tree proof + execution proof → single verifiable proof

### Phase 4 — Anchoring + Data Publishing
- [ ] L1 anchor: BatchInfo SSZ + state root commitment on-chain
- [ ] Blob encoding: BinaryTrieStateDiff batches → EIP-4844 blobs
- [ ] External DA pointers: Celestia/EigenDA for overflow
- [ ] Anchor contract: verify batch hash + state root on L1
- [ ] BAL consumption (EIP-7928): produce trie diffs from BALs without re-execution

### Phase 5 — Gossip + Light Client Sync
- [ ] Topic-based gossip:
  - `chain/{id}/blocks/full` — full BinaryTrieStateDiff
  - `chain/{id}/blocks/roots` — roots only (light clients pull diffs on demand)
  - `chain/{id}/contract/{address}` — server-side filtered diffs
- [ ] Per-contract light client sync protocol:
  - Cold start: `eth_getContractStems(address)` or incremental discovery via gossip
  - Ongoing: filtered diff subscription
  - Verification: each diff proven against published state root
- [ ] RocksDB / SQLite persistent `IBinaryTrieNodeStore` for full nodes

## Per-Contract Light Client — The Key Use Case

A wallet or DApp syncs only the contracts it cares about and nothing else. Fully verified against the global state root.

**Initial sync (USDC example):**
1. Request all stems for address = USDC → ~8-12M stems (~1.2 GB with proofs)
2. Or: just the account stem + specific holders on demand (992 bytes each)
3. Each proof verified against the published state root — trustless

**Ongoing sync:**
1. Subscribe to filtered diff topic for USDC
2. Per block: receive only USDC stems that changed (~2-4 KB)
3. Verify proof, update local state
4. No RPC trust dependency — the wallet IS a verified light client

**Composable:** subscribe to USDC + WETH + Uniswap → verified DeFi state in ~3 GB.

## Realistic Sizing

| Scenario | Stems touched | Full diff | Per-contract filtered |
|----------|---------------|-----------|----------------------|
| Simple transfer | 2 | ~1.2 KB | ~1.2 KB |
| Realistic DeFi block (200 txs) | ~500 | ~200 KB | ~2-4 KB |
| Heavy block (1000 txs) | ~2000 | ~800 KB | ~5-10 KB |
| Per day at 1 block/sec | | ~17 GB | ~350 MB per contract |

## Open Problems

### Gossip Heterogeneity
Three client types (full/light/filtered) need separate gossip channels. Relay infrastructure cost for filtered topics.

### Bootstrap / Cold-Start
New per-contract clients need an initial stem index. Options: address-index RPC, contract manifest registration, or incremental gossip discovery. All require a trusted state root from L1.

### DA Coordination
18-day blob retention → external archiving needed. Operator SLA for AppChains; economic incentives for decentralised chains. Architecture provides encoding + anchor format; DA layer is pluggable.

### Economic Sustainability
Tree proving (sequencer pays), EVM proving (outsourced market), L1 blob costs (batch submitter, amortised), archive storage (operator/treasury), relay infrastructure (fee market or subsidised).

## Architecture

```
Block Producer (Sequencer)
    │
    ├─ BinaryIncrementalStateRootCalculator
    │       └─ IBinaryTrieNodeStore (depth + address indexing)
    │               ├─ GetDirtyNodes() ─────────────────────┐
    │               ├─ GetNodesAtDepth(20) ──────┐          │
    │               └─ GetStemsByAddress(addr) ──│──────────│──► Per-contract subtree
    │                                            │          │
    ▼                                            ▼          ▼
BinaryTrieStateDiff (v1)                Full Light Client   Per-Contract Client
    │                                   (32 MB checkpoint)  (USDC wallet: ~1 MB)
    │                                        │                    │
    ├─ Gossip (Phase 5)                      │                    │
    ├─ Batch via BatchInfo (Phase 4)         │                    │
    └─ Anchor to L1/DA (Phase 4)             │                    │
                                             ▼                    ▼
                                   Apply full diff          Apply filtered diff
                                   (~200 KB/block)          (~2-4 KB/block)
                                             │                    │
                                             └───── both ─────────┘
                                                     │
                                               Verify against
                                             published state root

Tree-only Prover (Poseidon, local)         Execution Prover (Zisk, outsourced)
    │                                           │
    └─ Proves state root transition ◄──────────┘ Proves EVM correctness
                    │
                    ▼
            Composed Proof (verifiable on L1)
```
