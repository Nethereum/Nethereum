# Sub-project C — Proof Generation (Zisk)

**Date:** 2026-04-20
**Umbrella:** `2026-04-20-appchain-integration-umbrella.md`
**Status:** Design locked. Prerequisites DONE (binary trie + Poseidon state root, EIP-4844 blob submission for proof DA, SSZ hash_tree_root). Implementation plan to follow — blocked on A (config surface).

---

## Purpose

The existing Zisk pipeline already generates per-block proofs end-to-end — validated this session against BLS / KZG / P256 / SSTORE state tests. This sub-project designs the **operational shape** around that primitive: when proofs are generated, how they're stored, how failures are absorbed, how the sequencer stays SOLID when the prover lags.

## Prover / sequencer coupling

**Asynchronous decoupled.** Block seals immediately when the sequencer produces it; the prover follows at its own pace. Sequencer never waits for proofs.

- Block is sealed, witness captured at block-seal boundary, witness queued.
- Prover processes the queue at its own pace, in a separate process / service.
- Cross-chain withdrawals / anchor-confirmed-state gate on a `provenThrough` pointer, not on `sealedThrough` — naturally fits the ZK-optimistic anchor shape in B.
- Prover is a **separate failure domain** → independent HA.

## `ProofGeneration` knob

Cadence is a per-AppChain config value:

| Value | Meaning | Fits |
|---|---|---|
| `Off` | No automatic prover. DevChain + private consortium. | Private, dev |
| `OnDemand` | Prover runs only when `ProveBlocks(range)` is explicitly called. | Business processes, audit-driven |
| `Periodic(N)` | Prover runs every N blocks automatically. | Games, high-TPS |
| `Continuous` | Alias for `Periodic(1)` — every block. | Financial |

Template defaults:

- `AppChain-Financial` → `Continuous`
- `AppChain-Data` → `Periodic(1000)` or `OnDemand`
- `AppChain-Private` → `OnDemand` or `Off`
- `DevChain` → `Off`

Cross-chain operations that gate on validity (withdrawals, L1 messaging) **trigger an on-demand proof** for the relevant block range if no proof already covers it. The knob lives in C; the semantics live in F.

## Prover process topology

`cargo-zisk prove` is a Rust binary. Proofs are CPU / memory-heavy (tens of GB RAM during proving, sustained CPU for seconds-to-minutes per block). The prover runs as a **separate `Nethereum.AppChain.Prover` process**, Rust-powered under the hood. Not embedded in the sequencer.

**Transport to / from prover: gRPC over TCP, always.**

- Localhost (prover on same box) and remote (prover on separate hardware / GPU / multi-tenant service) are a *deployment* difference, not a code difference.
- TLS on by default; off for localhost if operator chooses.
- Same `Nethereum.AppChain.Prover` client used in both cases.

## Proof artifact

### Format — SSZ envelope

```
ProofArtifact (SSZ StableContainer):
  version:           u16
  proof_bytes:       List[byte, MAX_PROOF_SIZE]       // raw Zisk STARK
  public_inputs:     Container
      pre_state_root:  bytes32
      post_state_root: bytes32
      block_range:     Range(u64, u64)
      block_hashes:    List[bytes32, MAX_BLOCKS_PER_PROOF]
      anchor_hash:     bytes32                         // for anchoring
  metadata:          Container
      elf_hash:        bytes32                         // verifier version lock
      zisk_version:    string
      generated_at:    u64
      prover_id:       bytes20
```

### Witness artifact — SSZ envelope around existing `BinaryBlockWitness`

Same storage backend and lifecycle contract as `ProofArtifact`.

### Storage backends

Same pattern as `Nethereum.AppChain.Anchoring.Postgres`:

| Backend | Default for | Characteristics |
|---|---|---|
| **Postgres** (`Nethereum.AppChain.Prover.Postgres`) | Default | Mirrors anchoring infra; queryable metadata; blobs up to a few MB per row |
| **Filesystem** | No-DB deployments | Content-addressed by proof hash; simple; needs shared FS for multi-node |
| **Object store (S3-compatible)** | Scale / archive | Metadata in DB, bytes in S3; deferred, optional |
| **DA-layer upload (sub-project G)** | Permanent archive | Manifest on-chain, bytes in G-backends (IPFS / AWS / torrent / NZB / Arweave) |

Operator picks via the `ProofStorage` knob (and `WitnessStorage` — same values).

## `WitnessRetention` knob

Per-chain lifecycle policy:

| Value | Fits |
|---|---|
| `UntilProven` | `Continuous` mode, no replay needed |
| `Days(N)` | Data AppChains with dispute windows |
| `Blocks(N)` | Fixed block-count retention |
| `Forever` | Financial AppChains, audit trails |

**Validator rule**: `ProofGeneration = OnDemand` requires `WitnessRetention ≠ UntilProven` — otherwise an on-demand proof for an old block would have no witness to work from. Enforced in `ChainConfigValidator` (A).

Template defaults:

- `AppChain-Financial` → `Forever`
- `AppChain-Data` → `Blocks(100_000)` (~1 day at 1s blocks)
- `AppChain-Private` → `UntilProven`
- `DevChain` → `UntilProven`

## Parallelism

**Single prover worker as default; advanced deployments scale to a pool of N workers, load-balanced through the queue.**

- Default = one worker, FIFO queue. Simplest, fine for chains whose `block_time × throughput < prove_time` (slow blocks or sparse traffic).
- Scale by deploying additional workers pointing at the same Postgres queue. Lease-based job pull ensures no duplicate work.
- Workers are identical; no sharding / pinning. Any worker can prove any block.

### Queue semantics

- Postgres-backed job queue.
- **Lease with visibility timeout** — a worker leases a job for *T* seconds; if no progress, another worker picks it up.
- **Dead-letter after K retries** — a witness that consistently crashes the prover goes to a DLQ; operator alerted.
- **Checkpoint-safe** — prover restart reads the queue; no in-memory state loss because witnesses live in storage.

### Backpressure

Sequencer does not throttle. Prover lag is surfaced via metric `proverLagBlocks`. Operator decides whether to alert or auto-scale the worker pool. In the extreme, the queue grows unboundedly, which surfaces as the witness table's size in Postgres — still a monitored metric.

## Determinism & ELF hash pinning

- Same `(witness, ELF)` → bit-identical proof (Zisk is deterministic; we don't add non-determinism).
- Every proof records the `sha256` of the Zisk ELF that produced it.
- **Verifier contracts (sub-project D) are registered per-ELF-hash.** A fork upgrade = new ELF = a new verifier contract deployed + the `SequencerRegistry` updated to point at it.
- Old proofs remain verifiable against their old ELF's verifier; upgrades don't invalidate history.

## Consumption API

Exposed by the prover service (gRPC):

```protobuf
service AppChainProver {
  // Prover side
  rpc PushWitness(Witness) returns (Accepted);      // sequencer pushes when block seals
  rpc ProveOnDemand(BlockRange) returns (JobId);    // caller requests an explicit prove
  rpc GetJobStatus(JobId) returns (JobStatus);

  // Consumer side
  rpc GetProof(BlockRangeQuery) returns (ProofArtifact);
  rpc StreamProofReady(Filter) returns (stream ProofReadyEvent);  // push notification
}
```

Consumers: anchor worker, aggregator (D), light clients, auditors. Same API for each.

## Cross-cutting concerns

### HA / failure modes

| Failure | Effect | Recovery |
|---|---|---|
| Sequencer crash mid-block | Witness for that block may not be queued | Replica takes over via leadership lease; replay from last-sealed block |
| Prover worker crash mid-proof | Job lease expires after *T* seconds | Another worker picks up the job; same witness, same ELF, same proof |
| All prover workers down | `proverLagBlocks` grows; no proofs generated | Sequencer unaffected; proofs resume on prover restart |
| Postgres outage | Queue + storage unavailable | Sequencer continues producing blocks in-memory; degraded mode with local witness cache; alerts operator |
| Stuck proof (infinite loop in guest) | Job never reports; lease expires; DLQ after K retries | Operator inspects DLQ; usually indicates witness bug or ELF regression |
| ELF hash mismatch on verification | Verifier rejects proof | Operator's deployment error; check ELF distribution |

### Observability (metric names)

- `prover.lag.blocks`
- `prover.proofs.generated.total`
- `prover.proofs.failed.total`
- `prover.queue.depth`
- `prover.queue.dlq.size`
- `prover.mean.prove.time.seconds`
- `prover.witness.retention.size.bytes`
- `prover.elf.hash` (label, not metric)

### Upgrade path

- Fork upgrade = new ELF binary. Old proofs remain valid against old verifier; new proofs use new verifier.
- Prover service picks up the new ELF via config; the new ELF's hash is recorded in every new proof's metadata.
- Rollback: if new ELF has a bug, deploy old ELF; re-prove failed blocks against old verifier; no data loss because witnesses are retained.

### Security boundaries

- Prover service trusts witnesses pushed by the sequencer (sequencer is the owner). No validation of witness content — the proof will either verify or fail.
- Consumers trust the proof (cryptographic), not the prover service.
- Prover service identity isn't cryptographically significant — anyone running the correct ELF on a correct witness produces the same proof. Prover ID in metadata is for operational tracking only.

### Scenario matrix (documentation deliverable)

| AppChain class | `ProofGeneration` | `WitnessRetention` | `ProofStorage` |
|---|---|---|---|
| DevChain | `Off` | `UntilProven` | `Filesystem` (no proofs anyway) |
| Private consortium | `OnDemand` | `Days(30)` | `Postgres` |
| Games | `Periodic(1000)` | `Blocks(100_000)` | `Postgres` + DA upload (via G) |
| Business process | `OnDemand` | `Days(365)` or `Forever` | `Postgres` |
| Financial | `Continuous` | `Forever` | `Postgres` + DA upload + cold archive |

## Critical files to create

| Path | Content |
|---|---|
| `src/Nethereum.AppChain.Prover/AppChainProverService.cs` | gRPC service host |
| `src/Nethereum.AppChain.Prover/ProverWorker.cs` | Single-worker pull loop |
| `src/Nethereum.AppChain.Prover/ZiskProverRunner.cs` | cargo-zisk subprocess wrapper |
| `src/Nethereum.AppChain.Prover.Postgres/PostgresWitnessQueue.cs` | Queue + lease + DLQ |
| `src/Nethereum.AppChain.Prover.Postgres/PostgresProofStore.cs` | Proof artifact storage |
| `src/Nethereum.AppChain.Prover.Filesystem/…` | Alt backend |
| `proto/appchain-prover.proto` | gRPC contract |
| `src/Nethereum.AppChain.Prover.Client/ProverClient.cs` | Typed client used by sequencer, anchor worker, aggregator |
| `tests/Nethereum.AppChain.Prover.Tests/…` | Integration tests (lease expiry, DLQ, multi-worker concurrency) |

## Non-goals for C

- On-chain verifier contract compilation — sub-project D.
- Aggregating many proofs — sub-project D.
- Publishing proofs to the swarm — sub-project E.
- Putting proofs on DA — sub-project G (uses C's artifacts).

## Verification

1. `ProofGeneration = Continuous` with a single worker handles a stream of blocks at 1 s/block interval (modest load) without lag growth over 24 hours.
2. `ProofGeneration = OnDemand` correctly retrieves a witness from `Blocks(N)` retention and generates a proof; rejects if `WitnessRetention = UntilProven`.
3. Prover kill-during-proof recovery: kill worker mid-job, a second worker picks up after lease timeout, same proof artifact produced.
4. DLQ test: inject a witness designed to fail the prover K+1 times; verify DLQ entry + operator alert metric.
5. ELF hash pinning: two proofs for the same block with different ELFs produce two different `ProofArtifact` metadata entries; verifiers selected by ELF hash correctly accept / reject.
6. Cross-process gRPC: prover runs on a different host than sequencer, everything works over TCP with TLS.
