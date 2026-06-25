# Nethereum.AppChain.Anchoring

> **PREVIEW** — APIs may change between releases.

Anchors AppChain state to any EVM chain (L1 or L2). Commits state roots, block hashes, and optionally compressed block data or ZK proofs to an on-chain anchor contract.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           YOUR APPCHAIN                                │
│  DevChain / CoreChain produces blocks with state roots                 │
└────────────┬────────────────────────────────────────────────────────────┘
             │ eth_getBlockByNumber (standard RPC)
             ▼
┌─────────────────────────┐     ┌──────────────────────────┐
│  IChainAnchorable       │     │  IAnchorSubmissionStrategy │
│  (RpcChainAnchorable)   │     │  7 strategies (DA × Proof) │
│  reads blocks via RPC   │     │  builds proof bytes        │
└────────────┬────────────┘     └────────────┬─────────────┘
             │                               │
             ▼                               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AnchorWorker (IHostedService)                                         │
│  Timer-based: every AnchorCadence blocks, fetches block, calls         │
│  strategy.BuildPayload(), submits via IAnchorService                   │
└────────────┬────────────────────────────────────────────────────────────┘
             │ submitAnchor(AggregatedAnchor, bytes proof)
             ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AppChainAnchor.sol (on anchor chain — L1 or L2)                       │
│  Validates: chain registered, proof system tier, block continuity      │
│  Stores: endBlock, endBlockHash, postStateRoot, blockHashesRoot        │
│  Emits: AnchorSubmitted event                                          │
└────────────┬────────────────────────────────────────────────────────────┘
             │ events
             ▼
┌──────────────────────┐    ┌──────────────────────────┐
│  Anchor Indexer      │    │  Explorer UI             │
│  indexes events      │───▶│  Nethereum.Explorer.     │
│  to Postgres         │    │  Anchoring (Razor CL)    │
└──────────────────────┘    └──────────────────────────┘
```

## Two Orthogonal Dimensions

Anchoring is configured via two independent enums:

### Data Availability — what block data is published

```csharp
public enum AnchoringDataAvailability : byte
{
    None = 0,          // anchor struct only, no block data in proof bytes
    Calldata = 1,      // brotli-compressed block RLP in proof bytes
    BlobReference = 2  // blob versioned hash in proof bytes (data in EIP-4844 blobs)
}
```

### Proof Mode — how execution is verified

```csharp
public enum AnchoringProofMode : byte
{
    None = 0,          // trust operator (commitment only)
    StarkHash = 1,     // STARK proof hash on-chain, proof in blobs, off-chain verification
    SnarkOnChain = 2   // SNARK proof on-chain, IVerifier.verify() called by contract
}
```

### On-Chain Proof System — maps to Solidity enum

```csharp
// C# (mirrors Solidity ProofSystem enum exactly)
public enum AnchoringOnChainProofSystem : byte
{
    NoProof = 0,           // commitment only
    StarkHashOffChain = 1, // STARK hash, off-chain verification
    SnarkOnChain = 2       // SNARK, on-chain verification
}
```

```solidity
// Solidity (source of truth)
enum ProofSystem {
    NoProof,           // 0
    StarkHashOffChain, // 1
    SnarkOnChain       // 2
}
```

Same names, same values. The C# enum byte IS the contract's proofSystem field.

## 7 Anchoring Strategies

Each strategy = DA mode × Proof mode. One class per combination, names tell you everything:

| Strategy class | DA | Proof | What it does |
|---|---|---|---|
| `AnchoringStrategy_NoDA_NoProof_CommitmentOnly` | None | None | Just the anchor struct. Cheapest. |
| `AnchoringStrategy_Calldata_NoProof_SyncOnly` | Calldata | None | Compressed blocks for node sync. No proof. |
| `AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable` | None | StarkHash | 32-byte STARK hash. Proof in blobs. |
| `AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable` | Calldata | StarkHash | Sync data + STARK hash combined. |
| `AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification` | None | SnarkOnChain | SNARK proof verified on-chain. Trustless. |
| `AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification` | Calldata | SnarkOnChain | Sync data + SNARK proof. |
| `AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA` | BlobRef | SnarkOnChain | Blob DA + SNARK proof. |

### Factory

```csharp
var strategy = AnchoringStrategyFactory.Create(
    AnchoringDataAvailability.Calldata,
    AnchoringProofMode.StarkHash);
// Returns: AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable
```

Invalid combinations are rejected at creation time:
- `BlobReference + None` → error (blob DA without proof is pointless)
- `BlobReference + StarkHash` → error (STARK proof is already in blobs)

## Quick Start

### Minimal — commitment only

```csharp
var config = new AnchorConfig
{
    Enabled = true,
    ChainId = 420420,
    TargetChainId = 10,  // Optimism
    TargetRpcUrl = "https://mainnet.optimism.io",
    AnchorContractAddress = "0xYourAnchorContract",
    SequencerPrivateKey = operatorKey,
    AnchorCadence = 100,
    AnchorIntervalMs = 60000,
    DataAvailability = AnchoringDataAvailability.None,
    ProofMode = AnchoringProofMode.None
};

var anchorService = new AppChainAnchorBatchService(config, web3, appChainId, genesisHash);
var chainAnchorable = new RpcChainAnchorable(appchainWeb3);
var worker = new AnchorWorker(chainAnchorable, anchorService, config);
await worker.StartAsync(cancellationToken);
```

### With compressed calldata for sync

```csharp
config.DataAvailability = AnchoringDataAvailability.Calldata;
// Strategy auto-selected: AnchoringStrategy_Calldata_NoProof_SyncOnly
```

### With STARK proof hash

```csharp
config.DataAvailability = AnchoringDataAvailability.None;
config.ProofMode = AnchoringProofMode.StarkHash;
// Strategy auto-selected: AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable
```

## Configuration

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `true` | Enable/disable the anchor worker |
| `ChainId` | BigInteger | `1337` | AppChain's chain ID |
| `TargetChainId` | BigInteger | — | Chain ID of the anchor target |
| `TargetRpcUrl` | string | — | RPC endpoint of the anchor chain |
| `AnchorContractAddress` | string | — | Deployed AppChainAnchor contract |
| `SequencerPrivateKey` | string | — | Operator key for signing |
| `AnchorCadence` | int | `100` | Anchor every N blocks |
| `AnchorIntervalMs` | int | `60000` | Timer poll interval (ms) |
| `MaxRetries` | int | `3` | Retry attempts per anchor |
| `RetryDelayMs` | int | `5000` | Base retry delay (exponential backoff) |
| `DataAvailability` | enum | `None` | DA mode (None, Calldata, BlobReference) |
| `ProofMode` | enum | `None` | Proof mode (None, StarkHash, SnarkOnChain) |

## On-Chain Contract

### AggregatedAnchor struct

```solidity
struct AggregatedAnchor {
    uint64  chainId;
    bytes32 genesisHash;
    uint64  startBlock;
    uint64  endBlock;
    uint8   anchorVersion;
    uint8   proofSystem;        // ProofSystem enum (0, 1, or 2)
    bytes32 endBlockHash;
    bytes32 previousAnchorHash; // chain continuity link
    bytes32 blockHashesRoot;    // Merkle root of block hashes
    bytes32 postStateRoot;
    bytes32 manifestHash;
}
```

### Contract validation (every anchor must pass all of these)

```
proof.length <= 4096 bytes
chain is registered
authority.canSubmitAnchor(chainId, msg.sender)
genesisHash matches registration
proofSystem >= chain's minimumProofSystem
anchorVersion >= chain's minimumAnchorVersion
schema exists for anchorVersion
startBlock == previousAnchor.endBlock + 1 (no gaps)
previousAnchorHash == previousAnchor.endBlockHash (chain continuity)
endBlock >= startBlock (non-empty range)
if requiresProof: IVerifier.verify(proof, publicInputs)
```

### Verifier contracts (one per proof system)

| ProofSystem | Verifier | requiresProof | Validates |
|---|---|---|---|
| 0 (NoProof) | address(0) | false | Nothing |
| 1 (StarkHashOffChain) | StarkBlobCommitmentVerifier | false | Hash format |
| 2 (SnarkOnChain) | PlonkVerifier (generated) | true | Cryptographic proof |

Additional format verifiers for DA: `CalldataFormatVerifier`, `PipelinePayloadVerifier`, `CalldataStarkVerifier`.

## Compression

Block data uses brotli compression via `CompressedEnvelope`:

```csharp
var envelope = CompressedEnvelope.Wrap(blockRlp, CompressionAlgo.Brotli);
var original = CompressedEnvelope.Unwrap(envelope);
```

Envelope format: `[version:1][compression_algo:1][compressed_data]`

## Restart Recovery

`AppChainAnchorBatchService.InitializeAsync()` reads `getLatestAnchor(chainId)` from the contract on startup. Recovers `_lastEndBlock` and `_lastEndBlockHash` so the first anchor after restart has correct `previousAnchorHash`. The contract enforces `previousAnchorHash == prev.endBlockHash` — without recovery, restart would fail.

## Component Projects

| Project | Purpose |
|---|---|
| `Nethereum.AppChain.Anchoring` | Core: strategies, worker, batch service, config |
| `Nethereum.AppChain.Anchoring.Postgres` | Indexer: EF entities, repositories, event processing |
| `Nethereum.Explorer.Anchoring` | UI: Razor Class Library with Blazor pages (pluggable) |
| `Nethereum.AppChain.Contracts` | Solidity: AppChainAnchor, verifiers, foundry tests |

### Pluggable Explorer UI

`Nethereum.Explorer.Anchoring` is a Razor Class Library. The explorer references it via `AddAdditionalAssemblies`:

```csharp
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(
        typeof(IAnchorExplorerService).Assembly);
```

Same pattern for future modules: Messaging, Proving, Admin.

## Aspire Integration

```csharp
// AppHost — anchor service with strategy configured via environment
var anchoring = builder.AddProject("anchoring")
    .WithReference(mainchain)
    .WithReference(appchain)
    .WithEnvironment("Anchoring__DataAvailability", "Calldata")
    .WithEnvironment("Anchoring__ProofMode", "None");
```

Status endpoint `GET /`:
```json
{
  "service": "Nethereum.AppChain.Anchoring",
  "anchorContract": "0xe7f1...",
  "cadence": 10,
  "intervalMs": 30000,
  "dataAvailability": "Calldata",
  "proofMode": "None"
}
```

## Cost Guide

| Scenario | Anchor target | DA | Proof | Monthly cost (1 block/s) |
|---|---|---|---|---|
| Development | — | None | None | $0 |
| Private consortium | L2 | None | None | ~$0.03 |
| Enterprise sync | L2 | Calldata | None | ~$0.10 |
| Enterprise verifiable | L1 | None | StarkHash | ~$10 + $0.15/proof |
| Financial (trustless) | L2 | None | SnarkOnChain | ~$52 |
| Full DA + proof | L2 | Calldata | StarkHash | ~$0.15 |

## Ad-Hoc Proofs

Proofs can also be submitted per-block independently of anchoring, via `AppChainProofManager`:
- `submitBlockProof(chainId, blockNumber, proof, proofSystem)` — operator or authorized prover
- `requestBlockProof{value: bond}(chainId, blockNumber)` — anyone posts bond
- `fulfillBlockProof(chainId, blockNumber, proof)` — prover earns bond

These are separate from anchor strategies — an AppChain can anchor with CommitmentOnly and still accept ad-hoc proofs for individual blocks.

## Related Packages

- **Nethereum.CoreChain** — Block headers, state management, compression, DA pipeline
- **Nethereum.Web3** — RPC interaction with anchor chain
- **Nethereum.Contracts** — Smart contract encoding for AppChainAnchor
- **Nethereum.Explorer** — Base explorer framework
