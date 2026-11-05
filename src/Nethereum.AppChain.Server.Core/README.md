# Nethereum.AppChain.Server.Core

> **PREVIEW** - This package is in preview. APIs may change between releases.

**The reusable composition behind the AppChain server: configuration, `AppChainComposition.ComposeAsync`, `AppChainServerRunner.RunAsync`, the HTTP/WebSocket endpoints, health checks, metrics and the `admin_*` RPC handlers**

[![NuGet](https://img.shields.io/nuget/v/Nethereum.AppChain.Server.Core.svg)](https://www.nuget.org/packages/Nethereum.AppChain.Server.Core/)

## Overview

`Nethereum.AppChain.Server.Core` is the library that the `Nethereum.AppChain.Server` command-line tool (`nethereum-appchain`) and Aspire hosts are built on. It turns an `AppChainServerConfig` into a running node: storage, chain, sequencer or Clique engine, DevP2P sync, cross-chain messaging, anchoring, MUD World deployment, JSON-RPC and metrics. Use it when you want to host an AppChain node inside your own process or application instead of launching the CLI. To run the node from a shell, use [Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md).

The package is an ASP.NET Core library (`Microsoft.NET.Sdk.Web`, `OutputType` `Library`) targeting `net10.0`. Public types live in the `Nethereum.AppChain.Server` namespace and its `Configuration`, `Hosting`, `Endpoints`, `Metrics`, `Rpc` and `Anchoring` child namespaces.

A node runs in one of three roles, decided by which keys or addresses the configuration holds:

| Role | Configuration |
|---|---|
| Sequencer | `Genesis.Owner` and `Consensus.Sequencer` hold private keys (`CanSign`) |
| Follower | `Genesis.Owner` and `Consensus.Sequencer` hold addresses only (`IsIdentified`) and sync is enabled |
| Clique signer | `Consensus.Mode = AppChainConsensusMode.Clique`, `Consensus.Clique.Signer` holds a private key |

## Installation

```bash
dotnet add package Nethereum.AppChain.Server.Core
```

## Dependencies

Project references: `Nethereum.AppChain`, `Nethereum.AppChain.Sequencer`, `Nethereum.AppChain.Anchoring`, `Nethereum.CoreChain.RocksDB`, `Nethereum.Consensus.Clique`, `Nethereum.DevP2P.Sync`, `Nethereum.ChainNode.Hosting`. Package references: `Microsoft.Extensions.Logging.Console`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`.

## Configure a node

`AppChainServerConfig` is the root. Its defaults come from the source: chain id `420420`, chain name `"AppChain"`, fork schedule hardfork `"amsterdam"` (`AppChainServerConfig.DefaultAppChainHardfork`), RPC port `8546`, data directory `./appchain-data`, DevP2P listen port `30403`, `Network.Serve = false`, discv4 and discv5 disabled.

```csharp
public class AppChainServerConfig
{
    public const string DefaultAppChainHardfork = "amsterdam";

    public BigInteger ChainId { get; set; } = 420420;
    public Nethereum.EVM.ChainForkSchedule ForkSchedule { get; set; }
    public string ChainName { get; set; } = "AppChain";
    public string? OtlpEndpoint { get; set; }
    public long MaxRequestBodySize { get; set; } = 10 * 1024 * 1024;
    public Nethereum.EVM.HardforkRegistry? Registry { get; set; }
    public ChainNodeConfig Node { get; set; }
    public AppChainGenesisConfig Genesis { get; set; }
    public AppChainConsensusConfig Consensus { get; set; }
    public AppChainAnchoringConfig Anchoring { get; set; }
    public AppChainMessagingConfig Messaging { get; set; }
    public AppChainMudConfig Mud { get; set; }

    public string RpcUrl { get; }
    public bool ImportsBlocks { get; }
    public bool ProducesBlocks { get; }
    public bool IsFullyUnconfigured { get; }

    public void DeriveAddresses()
    public void Validate()
}
```

- `ImportsBlocks` is true when `Node.Sync.Mode` is not `SyncMode.None`.
- `Registry` is the hardfork registry the execution core builds the chain configuration with; when it is null the default mainnet registry is used. `Nethereum.AppChain.Server` sets it to a registry with the BLS12-381 and KZG precompile backends (`AppChainPrecompileBackends.Build()`).
- `ProducesBlocks` is true when the signer from `Consensus.ResolveClique()` can sign: `Consensus.Clique.Signer` for Clique, `Consensus.Sequencer` otherwise.
- `IsFullyUnconfigured` is true when no role has an address or a private key.
- `Validate()` calls `AppChainServerConfigValidator.Validate`.

```csharp
public sealed class SignerKeyPair
{
    public string? PrivateKey { get; set; }
    public string? Address { get; set; }
    public bool CanSign { get; }
    public bool IsIdentified { get; }
    public void DeriveAddressFromPrivateKey()
}

public sealed class AppChainGenesisConfig
{
    public SignerKeyPair Owner { get; set; }
    public bool DeployCreate2Factory { get; set; } = true;
}

public sealed class AppChainConsensusConfig
{
    public AppChainConsensusMode Mode { get; set; } = AppChainConsensusMode.SingleSequencer;
    public SignerKeyPair Sequencer { get; set; }
    public int BlockTimeMs { get; set; } = 1000;
    public bool AllowEmptyBlocks { get; set; }
    public BlockProductionMode BlockProductionMode { get; set; } = BlockProductionMode.Interval;
    public PolicyConfig Policy { get; set; } = PolicyConfig.OpenAccess;
    public string? ProducerAuthorityFile { get; set; }
    public string? NodeIdentity { get; set; }
    public IProducerAuthority? ProducerAuthorityOverride { get; set; }
    public AppChainCliqueConfig Clique { get; set; }

    public AppChainCliqueConfig ResolveClique()
}

public sealed class AppChainCliqueConfig
{
    public SignerKeyPair Signer { get; set; }
    public string[] InitialSigners { get; set; } = Array.Empty<string>();
    public int PeriodSeconds { get; set; } = 15;
    public int EpochLength { get; set; } = 30000;
}

public enum AppChainConsensusMode { SingleSequencer, Clique }

public static class AppChainConsensusModeParser
{
    public const string SingleSequencerName = "single-sequencer";
    public const string CliqueName = "clique";
    public static AppChainConsensusMode Parse(string? name)
    public static string NameOf(AppChainConsensusMode mode)
}

public sealed class AppChainAnchoringConfig
{
    public string? L1RpcUrl { get; set; }
    public string? ContractAddress { get; set; }
}

public sealed class AppChainMessagingConfig
{
    public bool Enabled { get; set; }
    public string[] HubSourceChains { get; set; } = Array.Empty<string>();
    public int PollIntervalMs { get; set; } = 5000;
    public int MaxMessagesPerPoll { get; set; } = 100;
    public bool AcknowledgmentEnabled { get; set; }
    public int AcknowledgmentIntervalMs { get; set; } = 30000;
}

public sealed class AppChainMudConfig
{
    public bool DeployWorld { get; set; } = true;
    public byte[] WorldSalt { get; set; } = new byte[32];
}
```

`ResolveClique()` returns `Clique` in Clique mode. In single-sequencer mode it returns a Clique configuration whose `Signer` is `Sequencer`, whose only initial signer is the sequencer address, whose `PeriodSeconds` is 0 and whose `EpochLength` is `Clique.EpochLength`, so both modes are driven by one signer rule.

`AppChainConsensusModeParser.Parse` returns `SingleSequencer` for a null or empty name, matches `"single-sequencer"` and `"clique"` ignoring case, and throws `InvalidOperationException` for anything else. `Node` is a `Nethereum.ChainNode.Hosting.Configuration.ChainNodeConfig` (storage, network, sync, RPC, mempool and producer settings are documented with that package).

Derive the addresses from the private keys, then validate:

```csharp
var config = new AppChainServerConfig();
config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();

config.DeriveAddresses();

var ownerAddress = config.Genesis.Owner.Address;
var sequencerAddress = config.Consensus.Sequencer.Address;
var cliqueSignerAddress = config.Consensus.Clique.Signer.Address;
```

`DeriveAddressFromPrivateKey` only sets `Address` when the pair can sign and is not already identified, so an address that is already set is never overwritten.

Backed by `AppChainServerConfigTests.Given_OnlyPrivateKeys_When_AddressesAreDerived_Then_EveryRoleIsIdentified` (`DocSection.ChainInfrastructure`, `appchain-server-core`).

### Validation

```csharp
public static class AppChainServerConfigValidator
{
    public static void Validate(AppChainServerConfig config)
    public static bool FollowsAnotherNode(AppChainServerConfig config)
}
```

`Validate` throws `InvalidOperationException` (or `ArgumentNullException` for a null config). The rules visible in the source:

- chain id positive; RPC port between 1 and 65535; block time positive when `BlockProductionMode` is `Interval`;
- a node that cannot sign (`FollowsAnotherNode`) needs an identified `Genesis.Owner` and `Consensus.Sequencer`; a node that can sign needs private keys for both;
- discovery may not be requested, and `Node.Producer` and configured enodes are validated by `ChainNodeProducerValidator` and `ChainNodeEnodeValidator`;
- in single-sequencer mode an identified sequencer whose private key is set must have an `Address` that belongs to that key, because followers verify every block against that address;
- Clique additionally needs a signer private key (unless following), at least one initial signer, at least one DevP2P enode to reach, and a `Sync.Mode` other than `None`.

A sequencer configuration with both keys validates:

```csharp
var config = new AppChainServerConfig();
config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();

config.Validate();
```

Backed by `Given_ASequencerWithKeys_When_ItIsValidated_Then_ItIsAccepted`. A follower holds addresses only, imports blocks and names the peer to follow:

```csharp
var config = new AppChainServerConfig();
config.Genesis.Owner.Address = "0x1111111111111111111111111111111111111111";
config.Consensus.Sequencer.Address = "0x2222222222222222222222222222222222222222";
config.Node.Sync.Mode = ChainNode.Hosting.Configuration.SyncMode.ForwardExecute;
config.Node.Sync.FollowPeerEnode = "enode://" + new string('a', 128) + "@127.0.0.1:30303";

config.Validate();
```

Backed by `Given_AFollowerWithAddressesOnly_When_ItIsValidated_Then_ItIsAccepted`. A Clique signer needs initial signers, a peer to reach and a sync mode:

```csharp
var config = new AppChainServerConfig();
config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Mode = AppChainConsensusMode.Clique;
config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Clique.InitialSigners = new[] { "0x1111111111111111111111111111111111111111" };
config.Node.Network.TrustedPeers = new[]
{
    $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30404"
};

config.Validate();
```

Backed by `Given_CliqueReachingSiblingsOverDevP2P_When_ItIsValidated_Then_ItIsAccepted`. All four tests are in `tests/Nethereum.AppChain.IntegrationTests/AppChainServerConfigTests.cs`.

## Compose a node in process

```csharp
public static class AppChainComposition
{
    public static async Task<AppChainComposedNode> ComposeAsync(
        AppChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken ct)
}
```

`ComposeAsync` is the single composition used by `AppChainServerRunner.RunAsync` and by the repository's tests. It applies ephemeral dev keys if the config is fully unconfigured, derives addresses, validates, registers the fork schedule, starts the `ChainNode`, writes genesis, builds the sequencer or Clique engine, messaging and MUD deployment, and returns an `AppChainComposedNode`.

```csharp
var config = new AppChainServerConfig { ChainId = 420420900, ChainName = "CompositionTest" };
config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
config.Node.Storage.InMemory = true;
config.Node.Network.Serve = false;
config.Node.Sync.Mode = SyncMode.None;
config.Mud.DeployWorld = false;

var composed = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
try
{
    var appChain = composed.AppChain;
    var sequencer = composed.Sequencer;
    var stateRootCalculator = composed.StateRootCalculator;
}
finally
{
    await composed.DisposeAsync();
}
```

Backed by `AppChainCompositionTests.Given_TheProductionAppChainComposition_When_ATestComposesANode_Then_ItGetsTheSameObjectsRunAsyncBuilds` (`appchain-server-core`, tests in `tests/Nethereum.AppChain.IntegrationTests/AppChainCompositionTests.cs`). `ComposeAsync` sets `EthECKey.SignRecoverable = true`; the tests restore the previous value afterwards.

With no identity at all, `ComposeAsync` generates ephemeral keys and logs `AppChain dev mode: generated ephemeral keys (NOT for production)`. If only some identity is supplied, it does not mask the gap: it throws (for example "Sequencer address is required for follower mode").

```csharp
var config = new AppChainServerConfig
{
    ChainId = 420420800,
    ChainName = "EphemeralDevKeyTest",
    Node = new ChainNodeConfig
    {
        Rpc = new ChainNodeRpcConfig { Port = 8546 },
        Storage = new ChainNodeStorageConfig { InMemory = true },
        Network = new ChainNodeNetworkConfig
        {
            Serve = false,
            Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true },
        },
        Sync = new ChainNodeSyncConfig { Mode = SyncMode.None },
        Mempool = new ChainNodeMempoolConfig { EnableTrustedPeerAdmission = true },
    },
    Mud = new AppChainMudConfig { DeployWorld = false },
};

var composed = await AppChainComposition.ComposeAsync(config, loggerFactory, CancellationToken.None);

var produces = composed.Produces;
var ownerKey = config.Genesis.Owner.PrivateKey;
```

Backed by `AppChainEphemeralDevKeyTests.Given_NoIdentityFlagsAtAll_When_AppChainComposes_Then_ItGeneratesEphemeralKeysAndLogsDevMode` (`appchain-server-core`).

### The composed node

```csharp
public sealed class AppChainComposedNode : IAsyncDisposable
{
    public required Nethereum.ChainNode.Hosting.ChainNode ChainNode { get; init; }
    public required Nethereum.AppChain.AppChain AppChain { get; init; }
    public required AppChainNode Node { get; init; }
    public SequencerConfig? SequencerConfig { get; init; }
    public ISequencer? Sequencer { get; init; }
    public CliqueEngine? CliqueEngine { get; init; }
    public CliqueBlockProductionStrategy? CliqueStrategy { get; init; }
    public required IncrementalStateRootCalculator StateRootCalculator { get; init; }
    public required IFinalityTracker FinalityTracker { get; init; }
    public required IMessageResultStore MessageResultStore { get; init; }
    public required IMessageMerkleAccumulator MessageAccumulator { get; init; }
    public IMessageQueue? MessageQueue { get; init; }
    public IMessageProcessor? MessageProcessor { get; init; }
    public MudGenesisResult? MudResult { get; init; }
    public bool Imports { get; init; }
    public bool Produces { get; init; }
    public required AppChainMetricsBundle Metrics { get; init; }
    public IProducerAuthority? ProducerAuthority { get; init; }
    public string? ProducerAuthorityNodeId { get; init; }
    public IChainForkChoice? ForkChoice { get; init; }
    public IChainStoreBundle Bundle { get; }

    public async ValueTask DisposeAsync()
}

public sealed class AppChainMetricsBundle
{
    public required BlockProductionMetrics BlockProduction { get; init; }
    public required TxPoolMetrics TxPool { get; init; }
    public required RpcMetrics Rpc { get; init; }
    public required StorageMetrics Storage { get; init; }
    public required SyncMetrics Sync { get; init; }
    public required SequencerMetrics Sequencer { get; init; }
    public required HAMetrics HA { get; init; }
    public required AnchoringMetrics Anchoring { get; init; }

    public static AppChainMetricsBundle Create(AppChainServerConfig config)
}

public sealed class AppChainMessagingComposition
{
    public AppChainMessagingComposition(
        IMessageResultStore resultStore,
        IMessageMerkleAccumulator accumulator,
        IMessageQueue? queue,
        IMessageProcessor? processor)

    public IMessageResultStore ResultStore { get; }
    public IMessageMerkleAccumulator Accumulator { get; }
    public IMessageQueue? Queue { get; }
    public IMessageProcessor? Processor { get; }
}
```

`DisposeAsync` stops the sequencer, disposes an `IAsyncDisposable` producer authority and then the `ChainNode`.

## Run a node

```csharp
public static class AppChainServerRunner
{
    public static Task RunAsync(AppChainServerConfig config)
    public static async Task RunAsync(AppChainServerConfig config, CancellationToken cancellationToken)

    public static SourceChainConfig ParseHubSourceChainEntry(string entry)
    public static string[] TrySplitPipeDelimited(string entry)
    public static string[] TrySplitColonDelimited(string entry)
}
```

`ParseHubSourceChainEntry` parses a `Messaging.HubSourceChains` entry into a `SourceChainConfig` (`ChainId`, `RpcUrl`, `HubContractAddress`). It splits on `|` when the entry contains one, otherwise on the colons around the RPC URL (the last `:0x` or, failing that, the last `:`), and returns `null` when there are fewer than three parts or the chain id is not a number. Backed by `HubSourceChainEntryParsingTests` (`appchain-server-core`).

`RunAsync` composes the node, builds a `WebApplication`, registers messaging, wires RPC and maps the endpoints, then runs until shutdown. If any `Node.Maintenance` operation is requested it runs that operation against the storage and exits the process with its exit code instead. The endpoints it maps:

| Endpoint | Source |
|---|---|
| `POST /` JSON-RPC (single and batch) | `RpcHttpEndpoint.MapRpcHttpEndpoint` (`Nethereum.ChainNode.Hosting.Rpc`) |
| `/ws` WebSocket JSON-RPC and subscriptions | `WebSocketEndpoints.MapWebSocketEndpoint` |
| `GET /blocks/latest`, `/blocks/{blockNumber}`, `/blocks/{blockNumber}/full`, `/blocks/range?from=&to=`, `/finality`, `/finality/{blockNumber}` | `LiveBlockEndpoints.MapLiveBlockEndpoints` |
| `GET /health` | health checks |
| `GET /status` | `AppChainStatus` |

`POST /` and `/ws` serve the JSON-RPC modules in `Node.Rpc.Modules` (default `eth,net,web3`; `txpool`, `debug` and `admin` are opt-in), answer `403` to a `Host` header outside `Node.Rpc.AllowedHosts`, and a body larger than `MaxRequestBodySize` is refused. `/ws` also refuses a browser `Origin` that is not in `Node.Rpc.AllowedOrigins`. `WebSocketEndpoints.MapWebSocketEndpoint(this WebApplication app, WebSocketRpcHandler wsHandler, RpcHttpOptions options)` takes the same options.

```csharp
public class AppChainStatus
{
    public long ChainId { get; set; }
    public string ChainName { get; set; } = "";
    public long BlockNumber { get; set; }
    public string RpcUrl { get; set; } = "";
    public string ConsensusMode { get; set; } = "";
    public string P2PMode { get; set; } = "";
    public bool? IsCurrentSequencer { get; set; }
    public long? LeaseFencingToken { get; set; }
    public AccountsStatus? Accounts { get; set; }
    public ContractsStatus? Contracts { get; set; }
    public AnchoringStatus? Anchoring { get; set; }
}

public class AccountsStatus { public string GenesisOwner { get; set; } = ""; public string Sequencer { get; set; } = ""; }

public class ContractsStatus
{
    public string Create2Factory { get; set; } = "";
    public string WorldFactory { get; set; } = "";
    public string World { get; set; } = "";
    public string InitModule { get; set; } = "";
    public string AccessManagementSystem { get; set; } = "";
    public string BalanceTransferSystem { get; set; } = "";
    public string BatchCallSystem { get; set; } = "";
    public string RegistrationSystem { get; set; } = "";
}

public class AnchoringStatus
{
    public bool Enabled { get; set; }
    public string? L1RpcUrl { get; set; }
    public string? AnchorContract { get; set; }
    public long LastAnchoredBlock { get; set; }
}
```

## Hosting building blocks

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppChainServer(this IServiceCollection services, AppChainServerConfig config)
    public static RpcHandlerRegistry AddAdminHandlers(this RpcHandlerRegistry registry)
    public static IServiceCollection AddAppChainMetrics(this IServiceCollection services, AppChainServerConfig config)
    public static IServiceCollection AddAppChainOpenTelemetry(this IServiceCollection services, AppChainServerConfig config)
    public static IServiceCollection AddAppChainHealthChecks(this IServiceCollection services)
}
```

- `AddAppChainServer` registers the config, `MudWorldDeployer` and an `InMemoryFinalityTracker` as `IFinalityTracker`.
- `AddAdminHandlers` registers `AdminAddPeerHandler` (`admin_addPeer`), `AdminRemovePeerHandler` (`admin_removePeer`), `AdminPeersHandler` (`admin_peers`) and `AdminNodeInfoHandler` (`admin_nodeInfo`); `NodeInfoDto` and `PeerInfoDto` are the result shapes.
- `AddAppChainMetrics` registers one instance each of `BlockProductionMetrics`, `TxPoolMetrics`, `RpcMetrics`, `StorageMetrics`, `SyncMetrics`, `SequencerMetrics`, `HAMetrics`, `AnchoringMetrics` and a `MetricsConfig`.
- `AddAppChainOpenTelemetry` adds the meters `{ChainName}.CoreChain`, `.CoreChain.Detailed`, `.Sequencer`, `.Sequencer.Detailed`, `.Sync`, `.Sync.Detailed`, `.Anchoring`, `.Anchoring.Detailed` and an OTLP exporter (endpoint from `OtlpEndpoint` when set).
- `AddAppChainHealthChecks` adds the `sequencer` and `sync` checks.

```csharp
public class SequencerHealthCheck : IHealthCheck
{
    public SequencerHealthCheck(
        ISequencer? sequencer = null,
        IProducerAuthority? producerAuthority = null,
        string? ourNodeId = null)

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
}

public class SyncHealthCheck : IHealthCheck
{
    public SyncHealthCheck(IPeerPool? peers = null)

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
}
```

`SequencerHealthCheck` is `Healthy` ("Follower mode") with no sequencer. With a producer authority it is `Unhealthy` unless the authority names `ourNodeId`; otherwise it is `Healthy` with the current block number, or `Unhealthy` if reading it throws. `SyncHealthCheck` is `Healthy` with no peer pool, `Degraded` with no connected peers, and `Healthy` otherwise.

```csharp
var follower = new SequencerHealthCheck(sequencer: null);
var followerResult = await follower.CheckHealthAsync(new HealthCheckContext());

var check = new SequencerHealthCheck(sequencer, new StaticAuthority("A"), "A");
var result = await check.CheckHealthAsync(new HealthCheckContext());
```

Backed by `SequencerHealthCheckTests.Given_NoSequencerAtAll_When_HealthIsChecked_Then_ItIsHealthyFollowerMode` and `Given_TheProducerAuthorityNamesThisNode_When_HealthIsChecked_Then_ItIsHealthy` (`appchain-server-core`); `StaticAuthority` is the test's `IProducerAuthority` returning a fixed holder from `CurrentProducer()`.

### Execution core and follower

```csharp
public sealed class AppChainExecutionCore
{
    public AppChainExecutionCore(
        AppChainConfig chainConfig,
        IChainStoreBundle bundle,
        CliqueEngine? clique,
        ILoggerFactory loggerFactory,
        IFlushCadence? flushCadence = null)

    public static AppChainConfig ChainConfigFor(Configuration.AppChainServerConfig config)
    public static IFlushCadence? FlushCadenceFor(Configuration.AppChainServerConfig config)

    public Nethereum.EVM.IChainActivations Activations { get; }
    public IncrementalStateRootCalculator StateRoot { get; }
    public BlockCommit Commit { get; }
    public BlockExecutor Executor { get; }
    public SideChainSupport SideChain { get; }
    public CliqueEngine? Clique { get; }
    public CliqueConsensusBlockGate? CliqueGate { get; }

    public IBlockExecutor CreateImporter()
    public async Task ChainRewrittenAsync(CancellationToken ct)
}

public sealed class AppChainDevP2PFollower
{
    public AppChainDevP2PFollower(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node, ILoggerFactory loggerFactory,
        Consensus.Clique.CliqueEngine? clique = null,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public AppChainDevP2PFollower(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node, ILoggerFactory loggerFactory,
        AsyncGate? chainWriteGate,
        Consensus.Clique.CliqueEngine? clique = null,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public AppChainDevP2PFollower(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node, ILoggerFactory loggerFactory,
        AppChainExecutionCore core,
        AsyncGate? chainWriteGate,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public static Task RunAsync(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node,
        ILoggerFactory loggerFactory, CancellationToken ct,
        AppChainExecutionCore core,
        AsyncGate? chainWriteGate,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public static Task RunAsync(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node,
        ILoggerFactory loggerFactory, CancellationToken ct,
        Consensus.Clique.CliqueEngine? clique = null,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public static Task RunAsync(
        AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node,
        ILoggerFactory loggerFactory, CancellationToken ct,
        AsyncGate? chainWriteGate,
        Consensus.Clique.CliqueEngine? clique = null,
        IChainForkChoice? forkChoice = null,
        IMempoolReorgReconciler? mempoolReconciler = null)

    public async Task RunAsync(CancellationToken ct)

    public static StrictValidationPolicy StrictPolicy(AppChainServerConfig config, ILoggerFactory loggerFactory)
}
```

The static `RunAsync` overloads construct a follower with the matching constructor and run it. The follower waits for a snap-capable peer, bootstraps from a pivot when needed, then imports blocks through the core's importer; it returns without error if the node was composed without a sync stack.

```csharp
public sealed class AppChainDefinition : IChainDefinition
{
    public AppChainDefinition(
        AppChainServerConfig config,
        Func<IChainStoreBundle, CancellationToken, Task> ensureGenesis = null)

    public Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
    public async Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle)
    public ICanonicalStateRootSource CreateTip(PeerPoolManager pool)
}

public sealed class RocksDbCliqueSnapshotStore : ICliqueSnapshotStore
{
    public RocksDbCliqueSnapshotStore(RocksDbManager rocks)
    public void Save(CliqueSnapshot snapshot)
    public CliqueSnapshot? Load(byte[] blockHash)
}
```

`AppChainDefinition.CreateProfileAsync` throws `InvalidOperationException` when the store has no genesis block, and `CreateTip` returns `null`.

### Message result store

```csharp
public class RocksDbMessageResultStore : IMessageResultStore
{
    public RocksDbMessageResultStore(RocksDbManager manager)
    public Task StoreAsync(MessageResult result)
    public Task<MessageResult?> GetByMessageIdAsync(ulong sourceChainId, ulong messageId)
    public Task<IReadOnlyList<MessageResult>> GetAllBySourceChainOrderedByLeafIndexAsync(ulong sourceChainId)
    public Task<IReadOnlyList<MessageResult>> GetBySourceChainAsync(ulong sourceChainId, int offset, int count)
    public Task<IReadOnlyList<ulong>> GetSourceChainIdsAsync()
    public Task<int> GetCountAsync(ulong sourceChainId)
}
```

## Metrics

```csharp
public static class AppChainPrometheusMetricsExtensions
{
    public static TBuilder AddPrometheusMetrics<TBuilder>(this TBuilder builder, int metricsPort)
        where TBuilder : IHostApplicationBuilder

    public static WebApplication MapPrometheusMetrics(this WebApplication app, string host, int rpcPort, int metricsPort)
}

public class MetricsConfig
{
    public bool Enabled { get; set; } = true;
    public int CollectionIntervalMs { get; set; } = 5000;
}

public class MetricsCollector : BackgroundService
{
    public MetricsCollector(
        BlockProductionMetrics blockProduction,
        SyncMetrics sync,
        IAppChain appChain,
        MetricsConfig config,
        TxPoolMetrics? txPool = null,
        ITxPool? txPoolSource = null,
        ILogger<MetricsCollector>? logger = null)
}

public class SyncMetrics : IDisposable
{
    public SyncMetrics(string chainId, string name = "Nethereum", IMeterFactory? meterFactory = null)

    public string? CurrentMode { get; }
    public long LocalHeadValue { get; }
    public long RemoteHeadValue { get; }
    public long LagBlocksValue { get; }
    public long FinalizedHeadValue { get; }
    public long SoftHeadValue { get; }
    public long BatchImportsCount { get; }

    public void SetSyncMode(string mode)
    public void SetLocalHead(long blockNumber)
    public void UpdateSyncStatus(long localHead, long remoteHead, long finalizedHead)
    public void RecordBatchImport(long sizeBytes, double durationSeconds)
    public void RecordLiveBlock(double latencySeconds)
    public void Dispose()
}
```

`AddPrometheusMetrics` only adds the Prometheus exporter when `metricsPort > 0`; `MapPrometheusMetrics` returns immediately when it is `<= 0`, otherwise maps the scraping endpoint on that port.

## Tests

The runnable examples are tagged `[NethereumDocExample(DocSection.ChainInfrastructure, "appchain-server-core", ...)]` in `tests/Nethereum.AppChain.IntegrationTests`. The endpoints (`LiveBlockEndpoints`, `WebSocketEndpoints`), the `admin_*` handlers, `AppChainServerRunner.RunAsync`, `AppChainDevP2PFollower` and the metrics types are documented as declarations only; no runnable example in this README starts them.
