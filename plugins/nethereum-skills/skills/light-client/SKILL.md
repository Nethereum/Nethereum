---
name: light-client
description: Help users initialize and manage an Ethereum beacon chain light client for tracking finalized and optimistic headers. Use this skill whenever the user mentions beacon chain, light client, sync committee, BLS verification, TrustedHeaderProvider, LightClientService, consensus layer tracking, header staleness, or wants to follow the Ethereum consensus without a full node.
user-invocable: true
---

# Beacon Chain Light Client — Nethereum.Consensus.LightClient

The Nethereum light client tracks the Ethereum beacon chain by verifying sync committee BLS aggregate signatures rather than downloading every block. It provides trusted finalized and optimistic execution-layer headers that can be used for state proof verification without running a full node.

## When to Use This vs Verified State

- **Use this skill** when the user needs to initialize, configure, update, or manage the light client itself -- sync committee tracking, BLS verification, header freshness, state persistence, or multi-chain config.
- **Use the verified-state skill** when the user wants to verify account balances, nonces, storage, or contract code using `eth_getProof` against a trusted header. The verified-state skill consumes `ITrustedHeaderProvider` but does not manage the light client lifecycle.

## Required Packages

```bash
dotnet add package Nethereum.Beaconchain              # Beacon API client
dotnet add package Nethereum.Consensus.LightClient     # Light client service, store, config
dotnet add package Nethereum.Signer.Bls.Herumi         # BLS12-381 signature verification (native)
```

## Chain Configurations

| Chain | Chain ID | Genesis Validators Root |
|-------|----------|------------------------|
| Mainnet | 1 | `0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95` |
| Sepolia | 11155111 | `0xd8ea171f3c94aea21ebc42a1ed61052acf3f9209c00e4efbaaddac09ed9b8078` |
| Holesky | 17000 | `0x9143aa7c615a7f7115e2b6aac319c03529df8242ae705fba9df39b79c59fa8b1` |

The `CurrentForkVersion` changes with each consensus hard fork. Fetch it dynamically from the beacon API rather than hardcoding it.

## Initialization Pattern

```csharp
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;

// 1. Connect to a Beacon API endpoint
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");

// 2. Fetch the weak subjectivity root from the latest finality update
var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);
var weakSubjectivityRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

// 3. Configure for mainnet
var config = new LightClientConfig
{
    GenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95".HexToByteArray(),
    CurrentForkVersion = "0x06000000".HexToByteArray(),
    SlotsPerEpoch = 32,
    SecondsPerSlot = 12,
    WeakSubjectivityRoot = weakSubjectivityRoot
};

// 4. Initialize BLS verification
var nativeBls = new NativeBls(new HerumiNativeBindings());
await nativeBls.InitializeAsync();

// 5. Create and initialize the light client
var store = new InMemoryLightClientStore();
var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
await lightClient.InitializeAsync();

// 6. Inspect initial state
var state = lightClient.GetState();
Console.WriteLine("Finalized slot: " + state.FinalizedSlot);
Console.WriteLine("Block number: " + state.FinalizedExecutionPayload.BlockNumber);
Console.WriteLine("Block hash: " + state.FinalizedExecutionPayload.BlockHash.ToHex(true));
```

## Update Lifecycle

After initialization, the light client needs periodic updates to track the chain.

### UpdateAsync -- Sync Committee Period Catch-Up

Processes up to 4 sync committee period updates at a time. Call at startup or after the client has been offline for more than one period (~27 hours).

```csharp
var updated = await lightClient.UpdateAsync();
var state = lightClient.GetState();
Console.WriteLine("Current period: " + state.CurrentPeriod);
Console.WriteLine("Finalized slot: " + state.FinalizedSlot);
```

### UpdateFinalityAsync -- Latest Finalized Header

Advances the finalized header without changing the sync committee. Call every few minutes.

```csharp
var finalityUpdated = await lightClient.UpdateFinalityAsync();
var state = lightClient.GetState();
Console.WriteLine("Finalized block: " + state.FinalizedExecutionPayload.BlockNumber);
```

### UpdateOptimisticAsync -- Near-Head Header

Fetches the most recent optimistic header, typically seconds behind the chain head. Call every 12 seconds (once per slot) if near-head data is needed.

```csharp
var optimisticUpdated = await lightClient.UpdateOptimisticAsync();
var state = lightClient.GetState();
Console.WriteLine("Optimistic block: " + state.OptimisticExecutionPayload.BlockNumber);
```

## TrustedHeaderProvider

Wraps `LightClientService` and exposes `ITrustedHeaderProvider`, the interface consumed by `VerifiedStateService` for state proof verification.

```csharp
var trustedProvider = new TrustedHeaderProvider(lightClient);

var finalizedHeader = trustedProvider.GetLatestFinalized();
Console.WriteLine("Finalized block: " + finalizedHeader.BlockNumber);
Console.WriteLine("State root: " + finalizedHeader.StateRoot.ToHex(true));

var optimisticHeader = trustedProvider.GetLatestOptimistic();
Console.WriteLine("Optimistic block: " + optimisticHeader.BlockNumber);

// Historical block hash lookup (up to 256 recent blocks)
var blockHash = trustedProvider.GetBlockHash(finalizedHeader.BlockNumber);
```

### Staleness Detection

Monitor header freshness and react when headers become stale.

```csharp
trustedProvider.FinalizedStalenessThreshold = TimeSpan.FromMinutes(30);
trustedProvider.OptimisticStalenessThreshold = TimeSpan.FromMinutes(5);

trustedProvider.StaleHeaderDetected += (sender, args) =>
{
    Console.WriteLine(args.HeaderType + " header is stale");
    Console.WriteLine("Age: " + args.Age.TotalMinutes + " minutes");
};

// Optionally throw instead of returning stale headers
trustedProvider.ThrowOnStaleHeader = true;
try
{
    var header = trustedProvider.GetLatestFinalized();
}
catch (StaleHeaderException ex)
{
    Console.WriteLine("Header age: " + ex.Age.TotalMinutes + " minutes");
}
```

## State Persistence (ILightClientStore)

The `ILightClientStore` interface controls how light client state is persisted across restarts. `InMemoryLightClientStore` is provided for testing. For production, implement a persistent store to avoid re-bootstrapping.

```csharp
public interface ILightClientStore
{
    Task<LightClientState?> LoadAsync();
    Task SaveAsync(LightClientState state);
}
```

File-based example:

```csharp
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
            return null;
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<LightClientState>(json);
    }

    public async Task SaveAsync(LightClientState state)
    {
        var json = JsonSerializer.Serialize(state);
        await File.WriteAllTextAsync(_filePath, json);
    }
}

// Usage
var store = new FileLightClientStore("light-client-state.json");
var lightClient = new LightClientService(beaconClient.LightClient, nativeBls, config, store);
await lightClient.InitializeAsync(); // Loads saved state if available
```

## Key Classes

| Class | Purpose |
|-------|---------|
| `LightClientService` | Core service: bootstrap, update, state management |
| `LightClientConfig` | Chain config: genesis root, fork version, slots/epoch |
| `LightClientState` | Full state: finalized/optimistic headers, sync committees, block hash history |
| `InMemoryLightClientStore` | In-memory `ILightClientStore` for testing |
| `TrustedHeaderProvider` | Simplified `ITrustedHeaderProvider` wrapper with staleness detection |
| `TrustedExecutionHeader` | BlockHash, BlockNumber, StateRoot, ReceiptsRoot, Timestamp |
| `BeaconApiClient` | Beacon chain REST API client |
| `LightClientResponseMapper` | Maps API responses to domain objects |
| `NativeBls` | BLS12-381 signature verification via Herumi |

## Common Gotchas

- **Staleness after inactivity**: If offline for more than one sync committee period (~27 hours), call `UpdateAsync` before finality/optimistic updates.
- **Native library loading**: `HerumiNativeBindings` loads a platform-specific native binary (`bls_eth.dll`/`libbls_eth.so`/`libbls_eth.dylib`). Ensure it is in the output directory.
- **Fork version changes**: `CurrentForkVersion` changes with each hard fork. Fetch dynamically from the beacon API state fork endpoint.
- **Archive node for proofs**: State proof verification via `eth_getProof` requires the execution node to have state at the block referenced by the light client header. Pruned nodes may not have it.

For full documentation, see: https://docs.nethereum.com/docs/consensus-light-client/guide-light-client
