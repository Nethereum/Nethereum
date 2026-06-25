# Nethereum.MainnetChain.Server

Read-only Ethereum mainnet follower host. Composes:

- `MainnetChainNode` (Nethereum.CoreChain) — the follower-only chain node bound to an `IChainStoreBundle` + `IBlockSource` + `IBlockExecutor` stack.
- DevP2P sync orchestration (Nethereum.DevP2P.Sync) — peer pool, fetcher, block source, listener.
- Optional beacon Light Client (Nethereum.Consensus.LightClient) — gates each imported block against the trusted finalized header per `consensus-specs/altair/light-client/sync-protocol.md`.
- JSON-RPC server (Nethereum.CoreChain.Rpc) — standard `eth_*` / `net_*` / `web3_*` handlers; `eth_getBlockByNumber` is overridden to resolve `"finalized"` / `"safe"` labels against the light-client state when active.

## Configuration

Configure via `appsettings.json` `"MainnetChain"` section, environment variables prefixed `MainnetChain__`, or command-line flags.

```json
{
  "MainnetChain": {
    "Host": "127.0.0.1",
    "Port": 8545,
    "DataDir": "./chaindata/mainnet",
    "TrustedPeer": null,
    "LightClient": {
      "BeaconEndpoint": "https://beacon-api.example.com",
      "WeakSubjectivityRoot": "0x...",
      "GenesisValidatorsRoot": "0x..."
    }
  }
}
```

When `LightClient.BeaconEndpoint` is absent the consensus gate degrades to `AlwaysAcceptConsensusBlockGate` and the RPC labels `"finalized"` / `"safe"` fall back to the latest committed block.

## Composition

```
WebApplicationBuilder
 └─ AddMainnetChainServer(config)
     ├─ IConsensusBlockGate            (AlwaysAccept | LightClient)
     ├─ IFinalityCursorProvider        (LatestOnly | LightClient)
     ├─ MainnetChainNodeFactory        (composes BlockExecutor + BlockImporter + ConsensusGatedBlockExecutor)
     ├─ MainnetChainNode               (FollowerService loop)
     ├─ MainnetChainHostedService      (drives RunAsync over IHostedService lifetime)
     ├─ LightClientHostedService       (initialise + poll updates — only when LC active)
     ├─ RpcDispatcher + handlers       (override eth_getBlockByNumber for finality labels)
     └─ Map "/" JSON-RPC (POST) + "/" liveness status (GET)
```

When `config.DataDir` is set, `AddMainnetChainServer` registers the chain store bundle and block source itself via `UseRocksDbAndDevP2PProductionComposition` — a RocksDB bundle + DevP2P block source composed in-project in `ProductionCompositionExtensions.cs`. Integration tests instead register an in-memory bundle + scripted source via `UseInMemoryBundleAndSource`.

## Related

- `tools/Nethereum.DevP2P.SyncNode` — the original from-genesis replay validator. This server mirrors that execution-stack composition in-project (no project reference to the tool).
- `src/Nethereum.CoreChain/MainnetChainNode.cs` — the engine.
- `src/Nethereum.Consensus.LightClient/LightClientService.cs` — beacon-chain light client.

## Deferred follow-ups

- Aspire AppHost integration
- Docker image
- `engine_*` Engine API
- Beacon REST proxy (currently the LC talks directly to its endpoint)
- AppChain consumer wiring
- Live mainnet soak harness
