# Nethereum.Beaconchain

.NET client for the Ethereum Beacon Chain REST API, providing access to consensus-layer state, light client bootstrap, sync committee updates, and finality data.

## Installation

```bash
dotnet add package Nethereum.Beaconchain
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Beaconchain
```

## Key Components

| Component | Description |
|-----------|-------------|
| `BeaconApiClient` | Main client for Beacon Chain REST API endpoints |
| `IBeaconApiClient` | Interface for the beacon client (supports DI and testing) |
| `LightClientApiClient` | Sub-client for light client specific endpoints (bootstrap, updates, finality) |
| `ILightClientApi` | Interface for light client API operations |
| `LightClientResponseMapper` | Maps JSON response DTOs to Nethereum.Consensus.Ssz domain models |
| `StateForkResponse` | DTO for beacon state fork information (previous/current version, epoch) |

## Quick Start

### Creating a BeaconApiClient

```csharp
using Nethereum.Beaconchain;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
```

You can also supply your own `HttpClient` or `IRestHttpHelper`:

```csharp
var httpClient = new HttpClient();
var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com", httpClient);
```

### Getting Fork Version

Retrieve the fork data for a given state (defaults to `"head"`):

```csharp
using Nethereum.Beaconchain;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");
var fork = await beaconClient.GetStateForkAsync();

var currentVersion = fork.Data.CurrentVersion;
var previousVersion = fork.Data.PreviousVersion;
var epoch = fork.Data.Epoch;
```

### Getting Finality Updates

```csharp
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");

var response = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(response);

var finalizedSlot = finalityUpdate.FinalizedHeader.Beacon.Slot;
var blockNumber = finalityUpdate.FinalizedHeader.Execution.BlockNumber;
var blockHash = finalityUpdate.FinalizedHeader.Execution.BlockHash.ToHex(true);
```

### Getting Optimistic Updates

```csharp
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");

var response = await beaconClient.LightClient.GetOptimisticUpdateAsync();
var optimisticUpdate = LightClientResponseMapper.ToDomain(response);

var attestedSlot = optimisticUpdate.AttestedHeader.Beacon.Slot;
var blockNumber = optimisticUpdate.AttestedHeader.Execution.BlockNumber;
var blockHash = optimisticUpdate.AttestedHeader.Execution.BlockHash.ToHex(true);
```

### Getting Light Client Bootstrap

Bootstrap requires a block root, typically obtained from a finality update:

```csharp
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");

var finalityResponse = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
var blockRoot = finalityUpdate.FinalizedHeader.Beacon.HashTreeRoot();

var response = await beaconClient.LightClient.GetBootstrapAsync(blockRoot.ToHex(true));
var bootstrap = LightClientResponseMapper.ToDomain(response);

var slot = bootstrap.Header.Beacon.Slot;
var syncCommitteePubKeyCount = bootstrap.CurrentSyncCommittee.PubKeys.Count;
var branchCount = bootstrap.CurrentSyncCommitteeBranch.Count;
```

### Getting Sync Period Updates

Fetch light client updates for one or more sync committee periods:

```csharp
using Nethereum.Beaconchain;
using Nethereum.Beaconchain.LightClient;

var beaconClient = new BeaconApiClient("https://ethereum-beacon-api.publicnode.com");

var finalityResponse = await beaconClient.LightClient.GetFinalityUpdateAsync();
var finalityUpdate = LightClientResponseMapper.ToDomain(finalityResponse);
var currentSlot = finalityUpdate.FinalizedHeader.Beacon.Slot;
var currentPeriod = currentSlot / (32 * 256);

var responses = await beaconClient.LightClient.GetUpdatesAsync(currentPeriod, 1);
var updates = LightClientResponseMapper.ToDomain(responses);

foreach (var update in updates)
{
    var attestedSlot = update.AttestedHeader?.Beacon?.Slot;
    var finalizedSlot = update.FinalizedHeader?.Beacon?.Slot;
}
```

## Response DTO Structure

The API returns JSON responses that are deserialized into DTO classes in the `Nethereum.Beaconchain.LightClient.Responses` namespace. Key types:

| DTO | Key Fields |
|-----|------------|
| `LightClientBootstrapResponse` | `Data.Header`, `Data.CurrentSyncCommittee`, `Data.CurrentSyncCommitteeBranch` |
| `LightClientUpdateResponse` | `Data.AttestedHeader`, `Data.NextSyncCommittee`, `Data.FinalizedHeader`, `Data.SyncAggregate`, `Data.SignatureSlot` |
| `LightClientFinalityUpdateResponse` | `Data.AttestedHeader`, `Data.FinalizedHeader`, `Data.FinalityBranch`, `Data.SyncAggregate` |
| `LightClientOptimisticUpdateResponse` | `Data.AttestedHeader`, `Data.SyncAggregate`, `Data.SignatureSlot` |
| `StateForkResponse` | `Data.PreviousVersion`, `Data.CurrentVersion`, `Data.Epoch` |
| `LightClientHeaderDto` | `Beacon` (slot, proposer index, roots), `Execution` (block number, hash, gas, timestamps), `ExecutionBranch` |

Use `LightClientResponseMapper.ToDomain()` to convert any response DTO into the corresponding domain model from `Nethereum.Consensus.Ssz` (e.g., `LightClientBootstrap`, `LightClientUpdate`, `LightClientFinalityUpdate`, `LightClientOptimisticUpdate`).

## Relationship to Other Packages

- **Nethereum.Consensus.Ssz** - Provides the domain model types (`LightClientBootstrap`, `LightClientUpdate`, `BeaconBlockHeader`, `ExecutionPayloadHeader`, `SyncCommittee`, `SyncAggregate`) that `LightClientResponseMapper` maps into. Also provides `HashTreeRoot()` for SSZ hash tree root computation.
- **Nethereum.Consensus.LightClient** - Implements the light client sync protocol (`LightClientService`) on top of this package. Uses `ILightClientApi` to fetch updates and `LightClientResponseMapper` to convert them for verification.
- **Nethereum.Signer.Bls.Herumi** - BLS signature verification used by `LightClientService` to validate sync committee signatures.
- **Nethereum.ChainStateVerification** - Uses the light client to obtain trusted headers and verify execution-layer state proofs (account balances, storage, code) against them.

## Additional Resources

- [Ethereum Beacon Chain API Specification](https://ethereum.github.io/beacon-APIs/)
- [Ethereum Light Client Specification](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/light-client/sync-protocol.md)
- [Nethereum Documentation](https://docs.nethereum.com/)
