# Sub-project E — Data Publishing + Sync + Verify

**Date:** 2026-04-20
**Umbrella:** `2026-04-20-appchain-integration-umbrella.md`
**Status:** Design locked. Blob DA primitives DONE (Transaction4844, BlobEncoder, SendBlobTransactionAsync, BeaconApiClient.GetBlobSidecarsAsync, DevChain IBlobStore + beacon REST). Implementation plan to follow — blocked on A (config surface).

---

## Purpose

Define what leaves the sequencer, how it travels to followers, how followers receive + verify. The three pillars are **data availability**, **transport / content-addressed retrieval**, and **follower verification**. Trust root for everything is the **on-chain commitment** — never the server that served the bytes.

## Data availability

`DataAvailability` knob — per-chain, set at genesis, overridable per hardfork.

| Value | Where data lives | Cost | Trust | Best for |
|---|---|---|---|---|
| `None` | Sequencer only; gossiped over libp2p; no on-chain commitment | Free | Trusted sequencer | Private / consortium |
| `Committee(N, K)` | Off-chain providers; N sign, K required to reconstruct | Cheap | N signers | Enterprise, games |
| `Calldata` | Parent chain, inside anchor tx calldata | Expensive | Parent chain | Small chains, bursty traffic |
| `Blobs` (EIP-4844) | Parent chain blob sidecar | Much cheaper than calldata for bulk | Parent chain | Financial / rollup-style |

First-class: `None / Committee / Calldata / Blobs`. **Pluggable** via `IDataAvailabilityProvider` interface — third-party DA (Celestia, EigenDA, Avail) and sub-project G's manifest-based backends land as plug-ins without core changes.

Template defaults:

- `AppChain-Financial` → `Blobs` (to L1)
- `AppChain-Data` → `Committee(5, 3)`
- `AppChain-Private` → `None`
- `DevChain` → `None`

## Transport — three-layer + manifest fallback

Every layer operates on **content-addressed data**: the payload hashes to a value, the on-chain commitment commits the hash, retrieval from *any source* is validated by re-hashing and comparing. This is the IPFS / content-addressable-storage pattern generalised: the hash is the address, the server isn't trusted.

| Layer | Protocol | What moves | Served by |
|---|---|---|---|
| **Real-time gossip** | libp2p gossipsub (via sidecar) | Newly-sealed blocks, proofs, receipts, anchors | Sequencer broadcasts; any peer relays |
| **Peer-to-peer exchange** | libp2p request/response or bitswap-style | Any historical item on demand | Any peer that has the data — not pinned to sequencer |
| **Sequencer pull endpoint** | .NET gRPC | Bootstrap, light-client queries, indexer catch-up | Sequencer only |
| **Manifest fallback (sub-project G)** | Content-addressed backend (AWS / NZB / torrent / IPFS / Arweave / par2-local) | Cold-archive catch-up, bulk historical data | Any G backend operator |

A new follower joining a 200-peer swarm gets historical data from across the swarm at ~200× bandwidth; the sequencer serves bootstrap + light-client traffic only. **Peers and G backends participate equally** in the retrieval protocol — anyone advertising hash X can serve it.

`P2PTransport` knob:

- `PullOnly` — gRPC pull endpoint on sequencer only. No libp2p sidecar. Private / consortium.
- `Hybrid` — libp2p sidecar (gossip + peer exchange) + pull endpoint. Default for public AppChains.

Defaults:

- `DevChain` → `PullOnly`
- `AppChain-Private` → `PullOnly`
- `AppChain-Data` / `AppChain-Financial` → `Hybrid`

### libp2p sidecar

Separate Go process (`Nethereum.AppChain.P2P.Sidecar` — replaces the current `Nethereum.AppChain.P2P.DotNetty`). Bridge to the .NET sequencer via gRPC-over-TCP. Same rationale as the prover: wire-level interop is TCP, localhost or remote is a deployment detail.

The sidecar handles peer discovery (kad-dht), NAT traversal, gossipsub fanout, noise / yamux, peer authentication (libp2p Ed25519 peer identity). Business-layer sequencer signing (secp256k1) is independent.

## Published topics

libp2p gossipsub topic names. Wire format matches the AppChain's A-mode — RLP for `Ethereum` / `EthereumBinaryV1`, SSZ for `RoadmapSszV1`. Envelope in all cases.

| Topic | Payload | Subscribers | Gossip / fetch-only |
|---|---|---|---|
| `/appchain/<chainId>/blocks/v1` | Sealed block header + body | Full nodes, indexers, light clients (header-only filter) | Gossiped |
| `/appchain/<chainId>/proofs/v1` | `ProofArtifact` | Light clients, anchor worker, auditors | Gossiped |
| `/appchain/<chainId>/anchors/v1` | Anchor commitment info | Everyone who cares about finality | Gossiped |
| `/appchain/<chainId>/txs/v1` | Pending mempool tx | Sequencer (incoming), peers for relay | Gossiped (optional) |
| `/appchain/<chainId>/diffs/v1` | State-diff / receipts bundle | Indexers | Fetch-only |
| `/appchain/<chainId>/witnesses/v1` | Full `BinaryBlockWitness` | External provers, auditors, full replay nodes | Fetch-only, often privileged |

Every payload is hashed and the gossip message is `(topic, hash, bytes)`. Receiver verifies hash, commits to local storage, can re-serve.

Witnesses are **not gossiped** (bandwidth-heavy, often private). Prover pulls witnesses from sequencer via C's gRPC contract. Only auditors / external verifiers consume the `/witnesses` fetch-only topic.

Light clients subscribe to `/blocks` with a header-only filter at the gossip layer — the sidecar offers this filter so bandwidth stays minimal for mobile / embedded clients.

`PublishTopics` knob — per-chain opt-in:

- `AppChain-Financial` → all six
- `AppChain-Data` → `Blocks` + `Proofs` + `Anchors` (drop `Txs` / `Diffs` / `Witnesses` for bandwidth)
- `AppChain-Private` → `Blocks` only
- `DevChain` → `Blocks` only (loopback gossip)

## Follower verification

`FollowerMode` knob — what the follower does with received data:

| Value | Verify approach | Overhead | Fits |
|---|---|---|---|
| `FullNode` | Replay every block; verify state root locally | High CPU | Archival, audit-grade |
| `LightClient` | Proof-verify only; trust proof → trust state root | Minimal | Mobile, embedded, light indexers |
| `TrustedReplica` | Signature-verify only; trust sequencer | Lowest | Inside trust boundary; HA replicas |

Defaults:

- `AppChain-Financial` / `AppChain-Data` / `DevChain` → `FullNode`
- `AppChain-Private` → `TrustedReplica`

## Sequencer identity + signing

**secp256k1** default, registered in an **`OnChainRegistry`** — matches every production EVM L2 (Arbitrum, Optimism, Polygon, zkSync). Pluggable interface for BLS (future decentralised sequencing) and Ed25519 (niche).

```
SequencerSignatureScheme = Secp256k1 | Bls | Ed25519        // default Secp256k1
SequencerRegistryMode    = GenesisPinned | OnChainRegistry  // default OnChainRegistry
```

### Registry rotation
- `OnChainRegistry` — predeployed contract holds the active key; governance-gated `rotate()` with a grace period where both old + new keys are valid (avoids propagation races).
- `GenesisPinned` — key immutable after genesis; rotation requires a hardfork. For sealed-key private AppChains.

### HA
- Single-silo active-passive: one key, shared across silo replicas via HSM / Key Vault / sealed secret. Failover transfers the lease, not the key.
- **libp2p peer identity is independent** — peers have Ed25519 libp2p IDs for transport; sequencer signing is secp256k1. Same separation as Ethereum's `enode` ID vs. user tx keys.

## Sync strategies

`SyncStrategy` knob — how a new follower catches up:

| Value | Mechanism | Assurance |
|---|---|---|
| `FullSync` | Replay every block from genesis | Maximum |
| `SnapSync` | Trust a recent snapshot (state root + proof + anchor commitment), sync forward | Same as full *if* proof + anchor verify; weaker if only sequencer-signed |
| `LightSync` | Headers + proofs only; read into state on-demand via Merkle proofs | Proof-gated |
| `LiveOnly` | Follow current gossip; don't backfill | Trusted sequencer |

Defaults by follower mode:

| FollowerMode | Default SyncStrategy |
|---|---|
| `FullNode` | `SnapSync` (fall back to `FullSync` if no snapshot) |
| `LightClient` | `LightSync` |
| `TrustedReplica` | `LiveOnly` |

### Snapshot trust bridge

Snapshot = `(block_number, state_root, proof_hash, anchor_tx_hash)`. Follower accepts a snapshot iff:

- `proof_hash` verifies locally (proof is valid for the stated state root) — primary path, objective trust, OR
- `anchor_tx_hash` confirmed on the parent chain with at least `K` confirmations — secondary path, parent-chain trust.

Peer serving the snapshot bytes is never trusted directly. The peer is just a source; trust comes from the on-chain commitment.

## Cross-cutting concerns

### HA / failure modes

| Failure | Effect | Recovery |
|---|---|---|
| Sequencer crash | Gossip stops; failover replica takes leadership | Followers keep peer-to-peer exchange working with cached blocks; gossip resumes when replica promotes |
| libp2p sidecar crash | Gossip stops; peer-to-peer exchange stops; pull endpoint still works | Sidecar auto-restart via process supervisor; followers retry connections |
| Swarm partition | Two sub-swarms; each sees partial gossip | Sub-swarms reconverge on partition heal; duplicate messages filtered by content hash |
| DA provider outage (committee / S3 / etc.) | New data unavailable at that provider | Fallback to other providers (for multi-provider DA); fallback to sequencer pull; raise alert |
| Follower behind by hours | Catch-up via SyncStrategy | Accept current snapshot; fast-forward; don't stall the gossip |
| Sequencer key compromise | Attacker could sign arbitrary blocks | `OnChainRegistry.rotate()` via governance; grace period honours old + new; followers pick up new key from the registry |

### Observability

- `p2p.peers.connected`
- `p2p.gossip.messages.sent/received/dedup`
- `p2p.exchange.requests.inbound/outbound`
- `sync.strategy.current`
- `sync.progress.blocks`
- `da.provider.health` (per provider)
- `sequencer.sig.verification.failures`
- `follower.mode` (label)

### Upgrade path

- Topic versioning (`/v1` → `/v2`) during a hardfork. Upgraders listen on both for a grace window.
- Sidecar upgrade: swap the sidecar binary while sequencer keeps running (different process).
- Sequencer sig scheme change: rotate via `OnChainRegistry` (not a hardfork, but gate followers behind the `grace period`).

### Security boundaries

- On-chain commitment is the sole trust root for received data.
- Sequencer sig authenticates "this block came from the legitimate sequencer" — prevents forged-sequencer DoS.
- Peer-to-peer exchange + G backends are **equal in trust** — both serve bytes; both are verified against the commitment.
- libp2p transport-layer auth (Ed25519 peer IDs, noise handshake) prevents traffic manipulation in transit but doesn't grant app-layer trust.

### Scenario matrix

| Class | `DataAvailability` | `P2PTransport` | `FollowerMode` default | `SyncStrategy` default |
|---|---|---|---|---|
| DevChain | `None` | `PullOnly` | `FullNode` | `FullSync` |
| AppChain-Private | `None` / `Committee(N, K)` | `PullOnly` | `TrustedReplica` | `LiveOnly` |
| AppChain-Data | `Committee(5, 3)` / `Calldata` | `Hybrid` | `FullNode` | `SnapSync` |
| AppChain-Financial | `Blobs` | `Hybrid` | `FullNode` | `SnapSync` |

## Critical files / projects

| Path | Change |
|---|---|
| `src/Nethereum.AppChain.P2P.Sidecar/` | New — Go libp2p sidecar (replaces DotNetty track) |
| `proto/appchain-sidecar.proto` | gRPC bridge contract |
| `src/Nethereum.AppChain.P2P/` | .NET client for the sidecar; content-addressed retrieval API |
| `src/Nethereum.AppChain.DataAvailability/IDataAvailabilityProvider.cs` | Interface |
| `src/Nethereum.AppChain.DataAvailability.Committee/…` | Built-in committee provider |
| `src/Nethereum.AppChain.DataAvailability.Calldata/…` | Built-in calldata provider |
| `src/Nethereum.AppChain.DataAvailability.Blobs/…` | Built-in blobs provider (EIP-4844) |
| `src/Nethereum.AppChain.Sync/FullSync.cs` / `SnapSync.cs` / `LightSync.cs` / `LiveOnly.cs` | Strategy implementations |
| `src/Nethereum.AppChain.Sequencer/SequencerRegistry/SequencerRegistryContract.sol` | On-chain registry |
| `src/Nethereum.AppChain.Sequencer/BlockSigning/…` | Scheme-pluggable signing |

## Non-goals for E

- Actual storage backend implementations (AWS / IPFS / torrent / NZB / par2) — sub-project G.
- Aggregator circuit + on-chain verifier — sub-project D.
- Anchoring contract schema — sub-project B.
- Cross-chain messaging — sub-project F.

## Verification

1. Two-node network (sequencer + one follower) exchanges blocks via libp2p gossip; follower verifies every block via configured `FollowerMode`.
2. Three-node network: follower B gets block N from follower A (not from sequencer) and validates — peer-to-peer exchange works.
3. New follower sync: stand up a node joining a chain with 10 000 existing blocks; `SnapSync` completes in bounded time; state roots match authoritative.
4. `LightClient` sync: subscribe to `/blocks` header-only; verify proofs from `/proofs`; state queries succeed via Merkle proof against anchored roots.
5. Sequencer key rotation: rotate via `OnChainRegistry`; followers accept blocks signed with new key after grace; reject after grace with old key.
6. Swarm partition test: disconnect half the peers; both halves continue gossiping within their partition; on reconnect, each half backfills from the other.
7. DA provider outage: simulate primary DA offline; follower falls back to sequencer pull; alert fires.
