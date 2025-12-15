# Nethereum.Consensus.LightClient

Ethereum beacon chain light client implementation for synchronizing with the consensus layer using minimal trust assumptions. Provides BLS signature verification, sync committee validation, and trusted execution header tracking for secure access to Ethereum state without running a full node.

## Installation

```bash
dotnet add package Nethereum.Consensus.LightClient
dotnet add package Nethereum.Signer.Bls.Herumi  # For BLS signature verification
```

**Native Library Requirement**: BLS signature verification requires the Herumi BLS native library (`bls_eth.dll`/`libbls_eth.so`/`libbls_eth.dylib`). The library is included in the `Nethereum.Signer.Bls.Herumi` NuGet package for Windows/Linux/macOS x64/arm64.

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
- Verifies 512-validator sync committee signatures (67% participation threshold)
- Tracks both finalized (2/3 finality) and optimistic (latest) headers
- Uses BLS12-381 signature aggregation for efficient verification

## Core Components

### LightClientService

Main orchestrator for light client synchronization (LightClientService.cs:14-393).

**Initialization:**

```csharp
// LightClientService.cs:36-68
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    _state = await _store.LoadAsync().ConfigureAwait(false);
    if (_state != null)
    {
        return; // Already initialized from persistent storage
    }

    // Bootstrap from weak subjectivity checkpoint
    var blockRootHex = _config.WeakSubjectivityRoot.ToHex(true);
    var response = await _apiClient.GetBootstrapAsync(blockRootHex).ConfigureAwait(false);
    var bootstrap = LightClientResponseMapper.ToDomain(response);
    ValidateBootstrap(bootstrap);

    _state = new LightClientState
    {
        FinalizedHeader = bootstrap.Header.Beacon,
        FinalizedExecutionPayload = bootstrap.Header.Execution,
        CurrentSyncCommittee = bootstrap.CurrentSyncCommittee,
        NextSyncCommittee = bootstrap.CurrentSyncCommittee,
        FinalizedSlot = bootstrap.Header.Beacon.Slot,
        CurrentPeriod = ComputePeriod(bootstrap.Header.Beacon.Slot),
        LastUpdated = DateTimeOffset.UtcNow
    };

    await _store.SaveAsync(_state).ConfigureAwait(false);
}
```

**Update Methods:**

1. **UpdateAsync** - Process finalized updates with sync committee rotation
2. **UpdateFinalityAsync** - Update finalized header only
3. **UpdateOptimisticAsync** - Update optimistic (latest) header

**BLS Signature Verification:**

```csharp
// LightClientService.cs:280-306
private bool VerifySyncAggregateCore(byte[] bits, byte[] signature, BeaconBlockHeader attestedHeader)
{
    if (bits.Length != SszBasicTypes.SyncCommitteeSize / 8)
    {
        return false;
    }

    // Select participating validators from sync committee
    var participants = SelectParticipantPubKeys(_state.CurrentSyncCommittee, bits);
    if (participants.Count == 0)
    {
        return false;
    }

    // Compute domain and signing root
    var domain = ComputeSyncCommitteeDomain();
    var message = ComputeSigningRoot(attestedHeader.HashTreeRoot(), domain);
    if (message == null || message.Length == 0)
    {
        return false;
    }

    // Verify BLS aggregate signature
    return _bls.VerifyAggregate(signature, participants.ToArray(), new[] { message }, domain);
}
```

**Participant Selection:**

```csharp
// LightClientService.cs:319-343
private List<byte[]> SelectParticipantPubKeys(SyncCommittee committee, byte[] bits)
{
    var pubKeys = committee?.PubKeys;
    var participants = new List<byte[]>();

    if (pubKeys == null || pubKeys.Count == 0 || bits == null)
    {
        return participants;
    }

    // Extract participating validators from bitfield
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

Tracks finalized and optimistic consensus state (LightClientState.cs:7-73).

```csharp
// LightClientState.cs:7-26
public class LightClientState
{
    public const int MaxBlockHashHistorySize = 256;

    public BeaconBlockHeader? FinalizedHeader { get; set; }
    public ExecutionPayloadHeader? FinalizedExecutionPayload { get; set; }
    public SyncCommittee? CurrentSyncCommittee { get; set; }
    public SyncCommittee? NextSyncCommittee { get; set; }

    public ulong FinalizedSlot { get; set; }
    public ulong CurrentPeriod { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public BeaconBlockHeader? OptimisticHeader { get; set; }
    public ExecutionPayloadHeader? OptimisticExecutionPayload { get; set; }
    public ulong OptimisticSlot { get; set; }
    public DateTimeOffset OptimisticLastUpdated { get; set; }

    public Dictionary<ulong, byte[]> BlockHashHistory { get; set; } = new Dictionary<ulong, byte[]>();
}
```

**Block Hash Management:**

```csharp
// LightClientState.cs:27-43
public void AddBlockHash(ulong blockNumber, byte[] blockHash)
{
    if (blockHash == null || blockHash.Length != 32) return;

    BlockHashHistory[blockNumber] = blockHash;

    // Automatic pruning when exceeding max size
    if (BlockHashHistory.Count > MaxBlockHashHistorySize)
    {
        PruneOldestEntries();
    }
}

public byte[] GetBlockHash(ulong blockNumber)
{
    return BlockHashHistory.TryGetValue(blockNumber, out var hash) ? hash : null;
}
```

### LightClientConfig

Configuration for light client initialization (LightClientConfig.cs:8-16).

```csharp
// LightClientConfig.cs:8-16
public class LightClientConfig
{
    public byte[] GenesisValidatorsRoot { get; set; } = Array.Empty<byte>();
    public byte[] CurrentForkVersion { get; set; } = new byte[4];
    public ulong SlotsPerEpoch { get; set; } = 32;
    public ulong SecondsPerSlot { get; set; } = 12;
    public byte[] WeakSubjectivityRoot { get; set; } = Array.Empty<byte>();
    public ulong WeakSubjectivityPeriod { get; set; } = 256 * 32; // ~27 hours
}
```

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
                $"{headerType} header is stale. Age: {age.TotalMinutes:F1} minutes, Threshold: {threshold.TotalMinutes:F1} minutes.",
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

## Usage Examples

### Example 1: Initialize Light Client with Real Mainnet Configuration

```csharp
using Nethereum.Consensus.LightClient;
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

// Mainnet configuration (LightClientLiveIntegrationTests.cs:289-299)
var config = new LightClientConfig
{
    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
    CurrentForkVersion = "0x06000000".HexToByteArray(), // Electra fork
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot
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
using Nethereum.ChainStateVerification;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Beaconchain;
using Nethereum.Signer.Bls.Herumi;

// Source: LightClientLiveIntegrationTests.cs:115-162

// Initialize light client (see Example 1)
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);
var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

var config = new LightClientConfig
{
    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
    CurrentForkVersion = "0x06000000".HexToByteArray(),
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot
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

```csharp
using Nethereum.Consensus.LightClient;
using System.IO;
using System.Text.Json;

// Custom persistent store
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

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<LightClientState>(json);
    }

    public async Task SaveAsync(LightClientState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_filePath, json);
    }
}

// Usage
var store = new FileLightClientStore("lightclient-state.json");
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

### Example 9: Complete Integration Example

```csharp
using Nethereum.Consensus.LightClient;
using Nethereum.Beaconchain;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Hex.HexConvertors.Extensions;

public class EthereumLightClient
{
    private readonly LightClientService _lightClient;
    private readonly TrustedHeaderProvider _headerProvider;
    private readonly CancellationTokenSource _cts = new();

    public EthereumLightClient(string beaconNodeUrl, byte[] checkpointRoot)
    {
        // Mainnet configuration
        var config = new LightClientConfig
        {
            GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
            CurrentForkVersion = "0x06000000".HexToByteArray(), // Electra
            SlotsPerEpoch = 32,
            SecondsPerSlot = 12,
            WeakSubjectivityRoot = checkpointRoot
        };

        var beaconClient = new BeaconApiClient(beaconNodeUrl);
        var nativeBls = new NativeBls(new HerumiNativeBindings());
        nativeBls.InitializeAsync().Wait(); // Initialize BLS in constructor
        var store = new FileLightClientStore("lightclient.json");

        _lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
        _headerProvider = new TrustedHeaderProvider(_lightClient)
        {
            ThrowOnStaleHeader = false
        };

        _headerProvider.StaleHeaderDetected += OnStaleHeader;
    }

    public async Task StartAsync()
    {
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
await client.StartAsync();

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

- **Nethereum.Beaconchain**: Beacon chain API client (ILightClientApi)
- **Nethereum.Consensus.Ssz**: SSZ container types (BeaconBlockHeader, SyncCommittee, etc.)
- **Nethereum.Signer.Bls**: BLS12-381 signature verification (IBls interface)
- **Nethereum.Signer.Bls.Herumi**: Native BLS implementation using Herumi library
- **Nethereum.Ssz**: SSZ serialization primitives (SszWriter, SszReader, SszMerkleizer)
- **Nethereum.ChainStateVerification**: Verified state queries with merkle proofs (VerifiedStateService)

## References

- [Light Client Sync Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/light-client/sync-protocol.md)
- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs)
- [Weak Subjectivity](https://ethereum.org/en/developers/docs/consensus-mechanisms/pos/weak-subjectivity/)
- [BLS Signatures](https://eth2book.info/capella/part2/building_blocks/signatures/)
- [Sync Committees](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/beacon-chain.md#sync-aggregate)
