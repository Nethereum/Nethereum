# Nethereum.Consensus.LightClient

Ethereum beacon chain light client implementation for synchronizing with the consensus layer using minimal trust assumptions. Provides BLS signature verification, sync committee validation, and trusted execution header tracking for secure access to Ethereum state without running a full node.

## Installation

```bash
dotnet add package Nethereum.Consensus.LightClient
dotnet add package Nethereum.Signer.Bls.Herumi  # For BLS signature verification
```

**Native Library Requirement**: BLS signature verification requires the Herumi BLS native library (`bls_eth.dll`/`libbls_eth.so`/`libbls_eth.dylib`). The library is included in the `Nethereum.Signer.Bls.Herumi` NuGet package for Windows x64, Linux x64/arm64, macOS x64/arm64 and Android arm64 (there is no Windows arm64 build).

## Overview

Nethereum.Consensus.LightClient implements the Ethereum light client sync protocol, enabling applications to verify beacon chain consensus with cryptographic security while maintaining minimal resource requirements. The light client tracks sync committees, verifies BLS aggregate signatures, and maintains finalized and optimistic execution headers.

**Key Features:**
- Bootstrap from weak subjectivity checkpoints
- BLS12-381 aggregate signature verification of sync committees
- Finalized and optimistic header tracking
- Automatic sync committee rotation across periods
- Block hash history (last 256 blocks)
- Staleness detection with configurable thresholds
- Persistent state storage abstraction

**Security Model:**
- Requires trusted weak subjectivity checkpoint to bootstrap
- Verifies 512-validator sync committee signatures (baseline participation floor for updates; 2/3 supermajority participation required for finality updates)
- Tracks both finalized (2/3 finality) and optimistic (latest) headers
- Uses BLS12-381 signature aggregation for efficient verification

## Core Components

### LightClientService

Main orchestrator for light client synchronization (LightClientService.cs:14-1151). Exposes the sync-committee domain type constant `public static readonly byte[] DomainSyncCommittee` (LightClientService.cs:23) and `public string LastRejectReason { get; private set; }` (LightClientService.cs:366), which records why the most recent update was rejected.

**Initialization:**

```csharp
// LightClientService.cs:43-115 (code verbatim; inline comments condensed)
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    _state = await _store.LoadAsync().ConfigureAwait(false);
    if (_state != null)
    {
        // Resumed from persistence: refresh to the current head using the resumed
        // committees. A transient light-client failure keeps the persisted head.
        try
        {
            await UpdateAsync(cancellationToken).ConfigureAwait(false);
            await UpdateFinalityAsync(cancellationToken).ConfigureAwait(false);
            await UpdateOptimisticAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Light client unreachable on resume — serve the persisted verified head.
        }
        return;
    }

    // Use the configured weak-subjectivity root, or derive it fresh from the
    // beacon node's latest finality update when none is configured.
    var bootstrapRoot = _config.WeakSubjectivityRoot;
    if (IsEmptyRoot(bootstrapRoot))
    {
        var finalityResponse = await _apiClient.GetFinalityUpdateAsync().ConfigureAwait(false);
        var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
        bootstrapRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();
    }
    var blockRootHex = bootstrapRoot.ToHex(true);
    var response = await _apiClient.GetBootstrapAsync(blockRootHex).ConfigureAwait(false);
    var bootstrap = LightClientResponseMapper.ToDomain(response);
    ValidateBootstrap(bootstrap, bootstrapRoot);

    _state = new LightClientState
    {
        FinalizedHeader = bootstrap.Header.Beacon,
        FinalizedExecutionPayload = bootstrap.Header.Execution,
        CurrentSyncCommittee = bootstrap.CurrentSyncCommittee,
        NextSyncCommittee = new SyncCommittee(),
        FinalizedSlot = bootstrap.Header.Beacon.Slot,
        CurrentPeriod = ComputePeriod(bootstrap.Header.Beacon.Slot),
        LastUpdated = DateTimeOffset.UtcNow
    };

    if (bootstrap.Header.Execution != null)
    {
        _state.SetBlockHash(
            bootstrap.Header.Execution.BlockNumber,
            bootstrap.Header.Execution.BlockHash,
            BlockHashFinality.Finalized);
    }

    await _store.SaveAsync(_state).ConfigureAwait(false);
}
```

**Update Methods:**

1. **UpdateAsync** - Process finalized updates with sync committee rotation
2. **UpdateFinalityAsync** - Update finalized header only
3. **UpdateOptimisticAsync** - Update optimistic (latest) header

**BLS Signature Verification:**

An update's sync aggregate is verified by `VerifyFullUpdateSyncAggregate` (LightClientService.cs:847-871), which enforces the baseline participation floor (and the supermajority quorum for finality updates) before delegating to `VerifyAggregateSignature`. That method selects the committee for the signature slot's period, derives the domain via `ComputeSyncCommitteeDomain(ulong signatureSlot)`, and verifies the BLS aggregate:

```csharp
// LightClientService.cs:936-972
private bool VerifyAggregateSignature(byte[] bits, byte[] signature, BeaconBlockHeader attestedHeader, ulong signatureSlot)
{
    if (bits == null || signature == null || attestedHeader == null)
    {
        return false;
    }

    // The sync aggregate is signed by the committee for the signature slot's sync-committee
    // period: the store's current committee when the signature period equals the store
    // (finalized) period, the next committee when it is period + 1. Verifying a next-period
    // signature against the current committee fails BLS and freezes the LC at every committee
    // boundary (process_light_client_update, specs/altair/light-client/sync-protocol.md).
    var signaturePeriod = ComputePeriod(signatureSlot);
    var storePeriod = ComputePeriod(_state.FinalizedSlot);
    SyncCommittee committee;
    if (signaturePeriod == storePeriod)
        committee = _state.CurrentSyncCommittee;
    else if (signaturePeriod == storePeriod + 1)
        committee = _state.NextSyncCommittee;
    else
        return false;
    if (committee == null)
    {
        return false;
    }

    var participants = SelectParticipantPubKeys(committee, bits);
    if (participants.Count == 0)
    {
        return false;
    }

    var domain = ComputeSyncCommitteeDomain(signatureSlot);
    var message = ComputeSigningRoot(attestedHeader.HashTreeRoot(), domain);

    return _bls.VerifyAggregate(signature, participants.ToArray(), new[] { message }, domain);
}
```

**Participant Selection:**

```csharp
// LightClientService.cs:985-1017
private List<byte[]> SelectParticipantPubKeys(SyncCommittee committee, byte[] bits)
{
    var pubKeys = committee?.PubKeys;
    var participants = new List<byte[]>();

    if (pubKeys == null || pubKeys.Count == 0 || bits == null)
    {
        return participants;
    }

    if (pubKeys.Count != SszBasicTypes.SyncCommitteeSize)
    {
        throw new InvalidOperationException(
            $"SyncCommittee.PubKeys must contain exactly {SszBasicTypes.SyncCommitteeSize} keys; got {pubKeys.Count}.");
    }

    EnsureCommitteeBitsLength(bits);

    var memberIndex = 0;
    for (var byteIndex = 0; byteIndex < bits.Length && memberIndex < pubKeys.Count; byteIndex++)
    {
        var value = bits[byteIndex];
        for (var bitIndex = 0; bitIndex < 8 && memberIndex < pubKeys.Count; bitIndex++, memberIndex++)
        {
            if ((value & (1 << bitIndex)) != 0)
            {
                participants.Add(pubKeys[memberIndex]);
            }
        }
    }

    return participants;
}
```

### LightClientState

Tracks finalized and optimistic consensus state (LightClientState.cs:33-126). Block hashes are stored with provenance (optimistic vs finalized) so a finalized entry cannot be overwritten by a conflicting optimistic write.

```csharp
// LightClientState.cs:15-52
public enum BlockHashFinality
{
    Optimistic = 0,
    Finalized = 1
}

public readonly struct ProvenancedBlockHash
{
    public ProvenancedBlockHash(byte[] blockHash, BlockHashFinality finality)
    {
        BlockHash = blockHash;
        Finality = finality;
    }

    public byte[] BlockHash { get; }
    public BlockHashFinality Finality { get; }
}

public class LightClientState
{
    public const int MaxBlockHashHistorySize = 256;

    public BeaconBlockHeader? FinalizedHeader { get; set; }
    public ExecutionPayloadHeader? FinalizedExecutionPayload { get; set; }
    public SyncCommittee? CurrentSyncCommittee { get; set; }
    public SyncCommittee? NextSyncCommittee { get; set; }

    public ulong FinalizedSlot { get; set; }
    public ulong CurrentPeriod { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.MinValue;

    public BeaconBlockHeader? OptimisticHeader { get; set; }
    public ExecutionPayloadHeader? OptimisticExecutionPayload { get; set; }
    public ulong OptimisticSlot { get; set; }
    public DateTimeOffset OptimisticLastUpdated { get; set; } = DateTimeOffset.MinValue;

    public Dictionary<ulong, ProvenancedBlockHash> BlockHashHistory { get; set; }
        = new Dictionary<ulong, ProvenancedBlockHash>();
    // ...
}
```

**Block Hash Management:**

```csharp
// LightClientState.cs:54-97
public void SetBlockHash(ulong blockNumber, byte[] blockHash, BlockHashFinality finality)
{
    if (blockHash == null || blockHash.Length != 32) return;

    if (BlockHashHistory.TryGetValue(blockNumber, out var existing))
    {
        if (existing.Finality == BlockHashFinality.Finalized)
        {
            if (!ByteArrayEquals(existing.BlockHash, blockHash))
            {
                if (finality == BlockHashFinality.Finalized)
                {
                    throw new InvalidOperationException(
                        $"Finalized block hash conflict at block {blockNumber}.");
                }

                return;
            }
        }
    }

    BlockHashHistory[blockNumber] = new ProvenancedBlockHash(blockHash, finality);

    if (BlockHashHistory.Count > MaxBlockHashHistorySize)
    {
        PruneOldestEntries();
    }
}

public void AddBlockHash(ulong blockNumber, byte[] blockHash)
    => SetBlockHash(blockNumber, blockHash, BlockHashFinality.Finalized);

public byte[]? GetBlockHash(ulong blockNumber)
{
    return BlockHashHistory.TryGetValue(blockNumber, out var entry) ? entry.BlockHash : null;
}

public byte[]? GetFinalizedBlockHash(ulong blockNumber)
{
    return BlockHashHistory.TryGetValue(blockNumber, out var entry)
           && entry.Finality == BlockHashFinality.Finalized
        ? entry.BlockHash
        : null;
}
```

### LightClientConfig

Configuration for light client initialization (LightClientConfig.cs:6-54). `GenesisValidatorsRoot` and `WeakSubjectivityRoot` are validating properties: each is backed by a 32-byte field (default `new byte[32]`) and the setter throws unless the assigned value is exactly 32 bytes. There is **no** `CurrentForkVersion` or `SlotsPerEpoch` on this type — fork versions and slots-per-epoch come from `ChainSpec` (defaulting to `ChainSpec.Mainnet`).

```csharp
// LightClientConfig.cs:6-54
public class LightClientConfig
{
    private byte[] _genesisValidatorsRoot = new byte[SszBasicTypes.RootLength]; // 32 bytes
    private byte[] _weakSubjectivityRoot = new byte[SszBasicTypes.RootLength];  // 32 bytes

    // Setter rejects any value whose length != 32.
    public byte[] GenesisValidatorsRoot
    {
        get => _genesisValidatorsRoot;
        set { /* null / length-32 validation */ _genesisValidatorsRoot = value; }
    }

    public ulong SecondsPerSlot { get; set; } = 12;

    // Setter rejects any value whose length != 32.
    public byte[] WeakSubjectivityRoot
    {
        get => _weakSubjectivityRoot;
        set { /* null / length-32 validation */ _weakSubjectivityRoot = value; }
    }

    public ulong WeakSubjectivityPeriod { get; set; } = 256 * 32;

    public ChainSpec ChainSpec { get; set; } = ChainSpec.Mainnet;
}
```

`ChainSpec` (in `Nethereum.Consensus.Ssz`) carries the fork-activation schedule and exposes `SlotsPerEpoch`, `SecondsPerSlot`, `GetForkAtSlot(ulong)` and `GetForkVersionAtSlot(ulong)`. The prebuilt `ChainSpec.Mainnet` maps each mainnet fork to its version (e.g. Electra = `0x05000000`, Fulu = `0x06000000`); the light client selects the correct fork version by slot internally, so applications normally leave `ChainSpec` at its default.

### TrustedHeaderProvider

Provides trusted execution headers with staleness detection (TrustedHeaderProvider.cs:6-103).

```csharp
// TrustedHeaderProvider.cs:6-31
public class TrustedHeaderProvider : ITrustedHeaderProvider
{
    private readonly LightClientService _lightClient;

    public TimeSpan FinalizedStalenessThreshold { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan OptimisticStalenessThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public bool ThrowOnStaleHeader { get; set; } = false;
    public event EventHandler<StaleHeaderEventArgs> StaleHeaderDetected;

    public TrustedHeaderProvider(LightClientService lightClient)
    {
        _lightClient = lightClient ?? throw new ArgumentNullException(nameof(lightClient));
    }

    public TrustedExecutionHeader GetLatestFinalized()
    {
        var state = _lightClient.GetState();
        if (state.FinalizedExecutionPayload == null || state.FinalizedHeader == null)
        {
            throw new InvalidOperationException("Light client state does not include a finalized execution payload yet.");
        }

        var header = MapToHeader(state.FinalizedExecutionPayload);
        ValidateStaleness(header, state.LastUpdated, FinalizedStalenessThreshold, "Finalized");
        return header;
    }
}
```

**Staleness Validation:**

```csharp
// TrustedHeaderProvider.cs:57-73
private void ValidateStaleness(TrustedExecutionHeader header, DateTimeOffset lastUpdated,
    TimeSpan threshold, string headerType)
{
    var age = DateTimeOffset.UtcNow - lastUpdated;

    if (age > threshold)
    {
        var args = new StaleHeaderEventArgs(headerType, age, threshold, header);
        StaleHeaderDetected?.Invoke(this, args);

        if (ThrowOnStaleHeader)
        {
            throw new StaleHeaderException(
                $"{headerType} header is stale. Age: {age.TotalMinutes:F1} minutes, Threshold: {threshold.TotalMinutes:F1} minutes. Call UpdateAsync() or UpdateFinalityAsync() to refresh.",
                age, threshold);
        }
    }
}
```

### ILightClientStore

Persistent storage abstraction for light client state (ILightClientStore.cs:5-10).

```csharp
// ILightClientStore.cs:5-10
public interface ILightClientStore
{
    Task<LightClientState?> LoadAsync();
    Task SaveAsync(LightClientState state);
}
```

**InMemoryLightClientStore:**

```csharp
// InMemoryLightClientStore.cs:8-19
public class InMemoryLightClientStore : ILightClientStore
{
    private LightClientState? _state;

    public Task<LightClientState?> LoadAsync() => Task.FromResult(_state);

    public Task SaveAsync(LightClientState state)
    {
        _state = state;
        return Task.CompletedTask;
    }
}
```

### TrustedExecutionHeader

Execution layer header extracted from consensus layer (TrustedExecutionHeader.cs:8-16).

```csharp
// TrustedExecutionHeader.cs:8-16
public class TrustedExecutionHeader
{
    public byte[] BlockHash { get; set; } = Array.Empty<byte>();
    public ulong BlockNumber { get; set; }
    public byte[] StateRoot { get; set; } = Array.Empty<byte>();
    public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
    public DateTimeOffset Timestamp { get; set; }
}
```

### LightClientStateCodec

Purpose-built binary serializer for `LightClientState` (LightClientStateCodec.cs:8-178). `LightClientState` holds SSZ containers (`BeaconBlockHeader`, `ExecutionPayloadHeader`, `SyncCommittee`) and a `Dictionary<ulong, ProvenancedBlockHash>` whose value is a `readonly struct`, so it does not round-trip through generic reflection-based serializers. Use this codec for persistence instead. The frame is `Magic("LCST") + Version + payloadLength + payload + CRC32(payload)`; each SSZ container is encoded fork-aware via its own `Encode`/`Decode`, and `TryDecode` returns `false` (never throws) on a truncated, corrupt, or CRC-mismatched buffer.

```csharp
// LightClientStateCodec.cs:13, 57
public static byte[] Encode(LightClientState state);
public static bool TryDecode(byte[] bytes, out LightClientState state);
```

### LightClientNetworks

Canonical per-network light-client constants (LightClientNetworks.cs:14-54). The `genesis_validators_root` is an immutable chain parameter fixed at genesis (it is part of the BLS signing domain, not a trust anchor), so it is baked in per network. The time-sensitive weak-subjectivity checkpoint root is deliberately **not** here — supply it out-of-band or let the client derive it from a trusted beacon endpoint.

```csharp
// LightClientNetworks.cs:16-42
public const string MainnetGenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95";
public const string SepoliaGenesisValidatorsRoot = "0xd8ea171f3c94aea21ebc42a1ed61052acf3f9209c00e4efbaaddac09ed9b8078";
public const string HoleskyGenesisValidatorsRoot = "0x9143aa7c615a7f7115e2b6aac319c03529df8242ae705fba9df39b79c59fa8b1";

// chainId: 1 mainnet, 11155111 sepolia, 17000 holesky. False for unknown chains.
public static bool TryGetGenesisValidatorsRoot(BigInteger chainId, out byte[] root);

// Builds a LightClientConfig with SecondsPerSlot = 12 and the network's genesis
// validators root (and the weak-subjectivity root when supplied).
public static LightClientConfig CreateConfig(BigInteger chainId, byte[] weakSubjectivityRoot = null);
```

## Usage Examples

### Example 1: Initialize Light Client with Real Mainnet Configuration

```csharp
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Hex.HexConvertors.Extensions;

// Source: LightClientLiveIntegrationTests.cs:76-112

// Get recent finalized checkpoint from beacon node
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);
var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

Console.WriteLine($"Using weak subjectivity root: {weakSubjectivityRoot.ToHex(true)}");

// Mainnet configuration. LightClientNetworks.CreateConfig(1, ...) is the shorthand:
//   var config = LightClientNetworks.CreateConfig(1, weakSubjectivityRoot);
// The explicit form below shows the fields it populates. ChainSpec defaults to
// ChainSpec.Mainnet (fork-version schedule + slots-per-epoch); set it only for a custom chain.
var config = new LightClientConfig
{
    GenesisValidatorsRoot = LightClientNetworks.MainnetGenesisValidatorsRoot.HexToByteArray(),
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot,
    ChainSpec = ChainSpec.Mainnet
};

// Initialize BLS verification with Herumi native library
var nativeBls = new NativeBls(new HerumiNativeBindings());
await nativeBls.InitializeAsync();

var store = new InMemoryLightClientStore();
var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);

// Bootstrap from checkpoint
await lightClient.InitializeAsync();

var state = lightClient.GetState();
Console.WriteLine($"Light client initialized at slot: {state.FinalizedSlot}");
Console.WriteLine($"Block number: {state.FinalizedExecutionPayload.BlockNumber}");
Console.WriteLine($"Block hash: {state.FinalizedExecutionPayload.BlockHash.ToHex(true)}");

// Apply updates
var updated = await lightClient.UpdateAsync();
Console.WriteLine($"Update applied: {updated}");
```

### Example 2: Update Light Client State

```csharp
using Nethereum.Consensus.LightClient;

// Periodic update loop
while (true)
{
    try
    {
        // Update finalized state (processes up to 4 periods)
        bool updated = await lightClient.UpdateAsync();

        if (updated)
        {
            var state = lightClient.GetState();
            Console.WriteLine($"Updated to slot {state.FinalizedSlot}");
            Console.WriteLine($"Current period: {state.CurrentPeriod}");
            Console.WriteLine($"Block number: {state.FinalizedExecutionPayload?.BlockNumber}");
        }
        else
        {
            Console.WriteLine("No updates available");
        }

        // Wait before next update (typical: every epoch or period)
        await Task.Delay(TimeSpan.FromMinutes(5));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Update failed: {ex.Message}");
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
}
```

### Example 3: Track Optimistic Headers

```csharp
using Nethereum.Consensus.LightClient;

// Update optimistic (latest) header more frequently
while (true)
{
    // Optimistic updates happen every slot (~12 seconds)
    bool updated = await lightClient.UpdateOptimisticAsync();

    if (updated)
    {
        var state = lightClient.GetState();
        Console.WriteLine($"Optimistic slot: {state.OptimisticSlot}");
        Console.WriteLine($"Optimistic block: {state.OptimisticExecutionPayload?.BlockNumber}");
        Console.WriteLine($"Age: {(DateTimeOffset.UtcNow - state.OptimisticLastUpdated).TotalSeconds:F1}s");
    }

    await Task.Delay(TimeSpan.FromSeconds(12)); // One slot
}
```

### Example 4: Access Trusted Execution Headers

```csharp
using Nethereum.Consensus.LightClient;

// Create trusted header provider
var headerProvider = new TrustedHeaderProvider(lightClient)
{
    FinalizedStalenessThreshold = TimeSpan.FromMinutes(30),
    OptimisticStalenessThreshold = TimeSpan.FromMinutes(5),
    ThrowOnStaleHeader = false
};

// Subscribe to staleness events
headerProvider.StaleHeaderDetected += (sender, args) =>
{
    Console.WriteLine($"{args.HeaderType} header is stale!");
    Console.WriteLine($"Age: {args.Age.TotalMinutes:F1} minutes");
    Console.WriteLine($"Threshold: {args.Threshold.TotalMinutes:F1} minutes");
};

// Get finalized header (2/3 finality guarantee)
var finalized = headerProvider.GetLatestFinalized();
Console.WriteLine($"Finalized block: {finalized.BlockNumber}");
Console.WriteLine($"Block hash: {finalized.BlockHash.ToHex(true)}");
Console.WriteLine($"State root: {finalized.StateRoot.ToHex(true)}");

// Get optimistic header (latest, may reorg)
var optimistic = headerProvider.GetLatestOptimistic();
Console.WriteLine($"Optimistic block: {optimistic.BlockNumber}");
Console.WriteLine($"Timestamp: {optimistic.Timestamp}");
```

### Example 5: Verified State Queries (Balance, Nonce, Storage)

```csharp
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.ChainStateVerification;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls.Herumi;

// This example additionally requires the Nethereum.ChainStateVerification and
// Nethereum.RPC packages, which are NOT dependencies of Nethereum.Consensus.LightClient
// (Nethereum.ChainStateVerification depends on this package, not the other way around).

// Source: LightClientLiveIntegrationTests.cs:115-162

// Initialize light client (see Example 1)
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);
var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

var config = new LightClientConfig
{
    GenesisValidatorsRoot = LightClientNetworks.MainnetGenesisValidatorsRoot.HexToByteArray(),
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot,
    ChainSpec = ChainSpec.Mainnet
};

var nativeBls = new NativeBls(new HerumiNativeBindings());
await nativeBls.InitializeAsync();

var store = new InMemoryLightClientStore();
var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
await lightClient.InitializeAsync();

// Create verified state service
var trustedProvider = new TrustedHeaderProvider(lightClient);
var rpcClient = new RpcClient(new Uri("https://mainnet.infura.io/v3/YOUR_KEY"));
var ethGetProof = new EthGetProof(rpcClient);
var ethGetCode = new EthGetCode(rpcClient);
var trieVerifier = new TrieProofVerifier();

var verifiedState = new VerifiedStateService(trustedProvider, ethGetProof, ethGetCode, trieVerifier);
verifiedState.Mode = VerificationMode.Finalized;

// Query account balance with cryptographic proof verification
var accountAddress = "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B";
var balance = await verifiedState.GetBalanceAsync(accountAddress);

var balanceInEth = (decimal)balance / 1_000_000_000_000_000_000m;
Console.WriteLine($"Account: {accountAddress}");
Console.WriteLine($"Balance: {balance} wei ({balanceInEth:F4} ETH)");

// Query nonce
var nonce = await verifiedState.GetNonceAsync(accountAddress);
Console.WriteLine($"Nonce: {nonce}");

// Get current header being verified against
var header = verifiedState.GetCurrentHeader();
Console.WriteLine($"Verified at block: {header.BlockNumber}");
Console.WriteLine($"State root: {header.StateRoot.ToHex(true)}");

// Optimistic mode for lower latency (may reorg)
await lightClient.UpdateOptimisticAsync();
verifiedState.Mode = VerificationMode.Optimistic;
var optimisticBalance = await verifiedState.GetBalanceAsync(accountAddress);
Console.WriteLine($"Optimistic balance: {optimisticBalance} wei");
```

**Note**: VerifiedStateService validates all data against merkle proofs from the light client's trusted state root. This provides trustless verification without requiring a full node.

**Archive Node Requirement**: State proof verification requires an archive node or a node with sufficient historical state. Standard pruned nodes may return errors like "missing trie node" or "old data not available due to pruning" for older blocks (LightClientLiveIntegrationTests.cs:369-372). Use recent finalized blocks or an archive node endpoint for historical queries.

### Example 6: Retrieve Block Hashes

```csharp
using Nethereum.Consensus.LightClient;

var headerProvider = new TrustedHeaderProvider(lightClient);

// Light client maintains last 256 block hashes
ulong blockNumber = 15537394;
byte[] blockHash = headerProvider.GetBlockHash(blockNumber);

if (blockHash != null)
{
    Console.WriteLine($"Block {blockNumber}: {blockHash.ToHex(true)}");
}
else
{
    Console.WriteLine($"Block {blockNumber} not in history");
}

// Access current state
var state = lightClient.GetState();
Console.WriteLine($"History size: {state.BlockHashHistory.Count}");
Console.WriteLine($"Finalized block: {state.FinalizedExecutionPayload?.BlockNumber}");
```

### Example 7: Persistent Storage Implementation

`LightClientState` holds SSZ containers and a `ProvenancedBlockHash` value struct, so it must be
persisted with the package's `LightClientStateCodec` (a general JSON serializer will not round-trip
it correctly). `Encode` returns a CRC32-framed, fork-aware binary blob; `TryDecode` returns `false`
on a truncated or corrupt file rather than throwing.

```csharp
using Nethereum.Consensus.LightClient;
using System.IO;
using System.Threading.Tasks;

// Custom persistent store backed by LightClientStateCodec
public class FileLightClientStore : ILightClientStore
{
    private readonly string _filePath;

    public FileLightClientStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<LightClientState?> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(_filePath);
        return LightClientStateCodec.TryDecode(bytes, out var state) ? state : null;
    }

    public async Task SaveAsync(LightClientState state)
    {
        var bytes = LightClientStateCodec.Encode(state);
        await File.WriteAllBytesAsync(_filePath, bytes);
    }
}

// Usage
var store = new FileLightClientStore("lightclient-state.bin");
var lightClient = new LightClientService(apiClient, bls, config, store);

// State persists across restarts
await lightClient.InitializeAsync();
```

### Example 8: Finality vs Optimistic Updates

```csharp
using Nethereum.Consensus.LightClient;

// Combined update strategy
async Task UpdateLightClientAsync()
{
    // Update finalized state (slower, ~6-13 minutes)
    bool finalityUpdated = await lightClient.UpdateFinalityAsync();

    if (finalityUpdated)
    {
        var state = lightClient.GetState();
        Console.WriteLine($"Finality updated to slot {state.FinalizedSlot}");
        Console.WriteLine($"Block {state.FinalizedExecutionPayload?.BlockNumber} is finalized");
    }

    // Update optimistic state (faster, ~12 seconds)
    bool optimisticUpdated = await lightClient.UpdateOptimisticAsync();

    if (optimisticUpdated)
    {
        var state = lightClient.GetState();
        Console.WriteLine($"Optimistic updated to slot {state.OptimisticSlot}");
        Console.WriteLine($"Latest block: {state.OptimisticExecutionPayload?.BlockNumber}");
        Console.WriteLine($"Note: May reorg before finality");
    }
}

// Finalized: Guaranteed by 2/3 validators (cannot reorg)
// Optimistic: Latest attestation (small reorg risk)
```

### Example 9: Sync Committee Period Rotation

```csharp
using Nethereum.Consensus.LightClient;

// Monitor sync committee rotation
var previousPeriod = lightClient.GetState().CurrentPeriod;

await lightClient.UpdateAsync();

var state = lightClient.GetState();
if (state.CurrentPeriod > previousPeriod)
{
    Console.WriteLine($"Sync committee rotated!");
    Console.WriteLine($"Old period: {previousPeriod}");
    Console.WriteLine($"New period: {state.CurrentPeriod}");
    Console.WriteLine($"Current committee updated");

    // Sync committees rotate every 256 epochs (~27 hours)
    // LightClientService automatically handles rotation
}
```

### Example 10: Complete Integration Example

```csharp
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Hex.HexConvertors.Extensions;

public class EthereumLightClient
{
    private readonly LightClientService _lightClient;
    private readonly TrustedHeaderProvider _headerProvider;
    private readonly NativeBls _nativeBls;
    private readonly CancellationTokenSource _cts = new();

    public EthereumLightClient(string beaconNodeUrl, byte[] checkpointRoot)
    {
        var config = new LightClientConfig
        {
            GenesisValidatorsRoot = LightClientNetworks.MainnetGenesisValidatorsRoot.HexToByteArray(),
            SecondsPerSlot = 12,
            WeakSubjectivityRoot = checkpointRoot,
            ChainSpec = ChainSpec.Mainnet
        };

        var beaconClient = new BeaconApiClient(beaconNodeUrl);
        _nativeBls = new NativeBls(new HerumiNativeBindings());
        var store = new FileLightClientStore("lightclient-state.bin");

        _lightClient = new LightClientService(beaconClient.LightClient, _nativeBls, config, store);
        _headerProvider = new TrustedHeaderProvider(_lightClient)
        {
            ThrowOnStaleHeader = false
        };

        _headerProvider.StaleHeaderDetected += OnStaleHeader;
    }

    public async Task InitializeAsync()
    {
        await _nativeBls.InitializeAsync();
    }

    public async Task StartAsync()
    {
        await InitializeAsync();
        await _lightClient.InitializeAsync(_cts.Token);
        Console.WriteLine("Light client started");

        // Background update loop
        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _lightClient.UpdateAsync(_cts.Token);
                    await _lightClient.UpdateOptimisticAsync(_cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Update error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(12), _cts.Token);
            }
        }, _cts.Token);
    }

    public TrustedExecutionHeader GetLatestBlock()
    {
        return _headerProvider.GetLatestOptimistic();
    }

    public TrustedExecutionHeader GetFinalizedBlock()
    {
        return _headerProvider.GetLatestFinalized();
    }

    public byte[] GetBlockHash(ulong blockNumber)
    {
        return _headerProvider.GetBlockHash(blockNumber);
    }

    private void OnStaleHeader(object sender, StaleHeaderEventArgs e)
    {
        Console.WriteLine($"Warning: {e.HeaderType} header is stale");
        Console.WriteLine($"Age: {e.Age.TotalMinutes:F1} min, Threshold: {e.Threshold.TotalMinutes:F1} min");
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        await Task.Delay(100);
        _cts.Dispose();
    }
}

// Usage - get checkpoint from beacon node first
var beaconApi = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var finalityUpdate = await beaconApi.LightClient.GetFinalityUpdateAsync();
var checkpoint = LightClientResponseMapper.ToDomain(finalityUpdate).FinalizedHeader.Beacon.HashTreeRoot();

var client = new EthereumLightClient("https://ethereum-beacon-api.publicnode.com", checkpoint);
await client.StartAsync(); // Calls InitializeAsync() then begins background updates

// Access trusted headers
var latest = client.GetLatestBlock();
Console.WriteLine($"Latest block: {latest.BlockNumber}");

var finalized = client.GetFinalizedBlock();
Console.WriteLine($"Finalized block: {finalized.BlockNumber}");
```

## Key Concepts

### Weak Subjectivity Checkpoint

Light clients require a **trusted checkpoint** to bootstrap securely:
- Must be a finalized beacon block root
- Should be from within the weak subjectivity period (~27 hours on mainnet)
- Prevents long-range attacks
- Can be obtained from trusted sources or checkpointz.ethstaker.cc

### Sync Committee

512 validators selected for light client consensus:
- Rotates every 256 epochs (~27 hours)
- Signs beacon block headers every slot
- BLS aggregate signatures verified by light client
- Requires 2/3 participation for security

### Finalized vs Optimistic Headers

**Finalized:**
- Guaranteed by beacon chain finality (2/3 validators)
- Cannot reorg under honest majority assumption
- Updated every ~6-13 minutes
- Used for high-security operations

**Optimistic:**
- Latest attested header
- May reorg before finality
- Updated every ~12 seconds
- Used for low-latency operations

### Sync Committee Periods

- Period length: 256 epochs (8192 slots, ~27 hours)
- Light client tracks CurrentSyncCommittee and NextSyncCommittee
- Automatic rotation handled by UpdateAsync()
- Period = slot / (32 * 256)

### Block Hash History

Light client maintains last 256 execution block hashes:
- Automatic pruning when limit exceeded
- Used for RPC call validation
- Indexed by execution block number
- Persisted in LightClientState

## Security Considerations

### Trust Assumptions

**Required Trust:**
- Initial weak subjectivity checkpoint must be trusted
- Beacon node API responses (signed data verified cryptographically)

**Cryptographic Security:**
- BLS12-381 aggregate signature verification
- SHA-256 merkle proof verification
- 2/3 honest validator assumption

### Attack Vectors

**Long-Range Attacks:**
- Mitigated by weak subjectivity checkpoint
- Must update within weak subjectivity period

**Eclipse Attacks:**
- Use multiple beacon node endpoints
- Verify responses across providers

**Staleness Attacks:**
- Detect with TrustedHeaderProvider staleness thresholds
- Configure appropriate update intervals

### Best Practices

1. **Update Frequency:**
   - Finalized: Every 6-13 minutes (2 epochs)
   - Optimistic: Every 12 seconds (1 slot)

2. **Checkpoint Management:**
   - Use recent finalized checkpoints (<27 hours old)
   - Store checkpoint sources for auditability

3. **Storage:**
   - Implement persistent ILightClientStore
   - Backup state regularly

4. **Error Handling:**
   - Retry failed updates with exponential backoff
   - Monitor staleness events
   - Handle network interruptions gracefully

## Dependencies

Direct project references (Nethereum.Consensus.LightClient.csproj):

- **Nethereum.Beaconchain**: Beacon chain API client (`ILightClientApi`, `LightClientResponseMapper`)
- **Nethereum.Consensus.Ssz**: Consensus SSZ container types and helpers (`BeaconBlockHeader`, `SyncCommittee`, `ExecutionPayloadHeader`, `ChainSpec`, `ConsensusFork`, `SszBasicTypes`, `LightClientForkSpec`)
- **Nethereum.Signer.Bls**: BLS12-381 signature verification abstraction (`IBls`)

`Nethereum.Ssz` (providing `SszMerkleizer`) arrives transitively through `Nethereum.Consensus.Ssz` and is not referenced directly.

Not dependencies of this package (add them separately when needed):

- **Nethereum.Signer.Bls.Herumi**: native Herumi BLS backend (`NativeBls`, `HerumiNativeBindings`). Consumers add this package to obtain a working `IBls` implementation (see Installation).
- **Nethereum.ChainStateVerification** and **Nethereum.RPC**: required only for Example 5 (`VerifiedStateService`, `EthGetProof`, `EthGetCode`). `Nethereum.ChainStateVerification` depends on this package, not the reverse, so the dependency direction is opposite to a normal prerequisite.

## References

- [Light Client Sync Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/light-client/sync-protocol.md)
- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs)
- [Weak Subjectivity](https://ethereum.org/en/developers/docs/consensus-mechanisms/pos/weak-subjectivity/)
- [BLS Signatures](https://eth2book.info/capella/part2/building_blocks/signatures/)
- [Sync Committees](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/beacon-chain.md#sync-aggregate)
