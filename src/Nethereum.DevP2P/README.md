# Nethereum.DevP2P

> **PREVIEW** — This package is in preview. APIs may change between releases.

Ethereum peer-to-peer transport and node discovery: RLPx framing + handshake, discovery (discv4, discv5, DNS ENR trees), and peer dial scheduling.

## Overview

Nethereum.DevP2P is the **transport and discovery** layer of a Nethereum node. It implements:

- **RLPx** — the encrypted TCP transport: ECIES handshake, AES-CTR + Keccak-MAC framing, and capability multiplexing (many sub-protocols over one socket).
- **Node discovery** — UDP **discv4** (Kademlia, signed) and **discv5** (authenticated WHOAREYOU/HKDF sessions with TalkReq/TalkResp), plus **DNS ENR-tree** discovery and hardcoded bootnodes.
- **Peering** — outbound dial scheduling with inbound/outbound ratio control, and layered inbound DoS hardening.

This package advertises the `eth/68`, `eth/69`, `eth/70`, `eth/71`, and `snap/1` capabilities (plus `snap/2` when `DevP2PConfig.AdvertiseSnap2` is set) and computes their message-id offsets, but it **does not contain the eth/snap protocol handlers or message classes**. The sub-protocol serving/sync logic lives in **Nethereum.DevP2P.Sync** (e.g. `PatriciaSnapRequestHandler`, `Eth68ServerSession`, `SnapSyncClient`, `SyncPeerSession`, `IEthPeer`), and the wire-message models live in **Nethereum.Model** (`Nethereum.Model.P2P`, with the snap/1 and snap/2 messages nested under `Nethereum.Model.P2P.Snap`). Think of this package as the pipe; the protocols that flow through it are layered on top.

## Where do I start?

Three tasks cover almost everything people come here to do. Each maps to one entry point with a complete, runnable example below — no undefined variables.

| I want to… | Entry point |
| --- | --- |
| **Dial a specific peer** (I already have its `enode://` URL) | `StaticPeerConnector.ConnectAsync` |
| **Listen for / accept inbound peers** | `RlpxListener` (`Start` + `PeerAccepted`) |
| **Discover new peers** (I don't have addresses yet) | `Discv5Discovery.StartMainnet` |

Every example needs a **node identity key** (a secp256k1 key that is your node id) and usually a **`DevP2PConfig`**. The two throwaway sources used below:

```csharp
using Nethereum.DevP2P;
using Nethereum.Signer;

// Node identity: generate a fresh key, or load your persisted node key.
var localKey = EthECKey.GenerateKey();

// Config quickstart — DevP2PConfig.ForDevChain(byte[] genesisHash, ulong networkId = 1337)
// collapses the sensible dev-chain knobs into one factory call.
var genesisHash = new byte[32];                       // your dev-chain genesis block hash
var config = DevP2PConfig.ForDevChain(genesisHash, networkId: 1337);
```

For a **stable enode across restarts** — required for a remote peer to durably trust your node by node-id — persist the node key instead of generating a fresh one each run. `NodeKeyStore.LoadOrCreate` loads a raw 32-byte private key from `path`, or generates and saves one on first run:

```csharp
using Nethereum.DevP2P.NodeDb;

// EthECKey LoadOrCreate(string path, Action<string> log = null)
var localKey = NodeKeyStore.LoadOrCreate("nodekey");
```

### Dial a peer — `StaticPeerConnector.ConnectAsync(enode, ct)`

Parses an `enode://` URL, opens the encrypted RLPx connection, and completes the handshake + Hello in one call.

```csharp
using System.Threading;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;

var localKey = EthECKey.GenerateKey();
var connector = new StaticPeerConnector(localKey);           // config optional; defaults are fine
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

// ConnectAsync(string enode, CancellationToken ct = default)
RlpxConnection conn = await connector.ConnectAsync(
    "enode://<64-byte-hex-pubkey>@127.0.0.1:30303",
    cts.Token);

// conn.SharedCapabilities now lists the negotiated protocols (eth >= 68).
```

### Listen / accept peers — `RlpxListener`

Accepts inbound connections and raises `PeerAccepted` once each peer is handshaked. The eth Status exchange and the per-protocol message loop are **not** here — that logic lives in **Nethereum.DevP2P.Sync**; wire this listener up to it (or to your own dispatch loop) inside the `PeerAccepted` handler.

```csharp
using System.Net;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;

var serverKey = EthECKey.GenerateKey();
var genesisHash = new byte[32];                              // your dev-chain genesis block hash
var config = DevP2PConfig.ForDevChain(genesisHash);

var listener = new RlpxListener(serverKey, config);
listener.PeerAccepted += (_, conn) =>
{
    // conn.SharedCapabilities is already negotiated. Run the eth Status
    // exchange and the protocol loop here — see Nethereum.DevP2P.Sync.
};

// Start(int port = 0, IPAddress bindAddress = null) — port 0 binds an ephemeral port.
listener.Start(port: 30303, bindAddress: IPAddress.Any);

// ... on shutdown:
await listener.StopAsync();                                 // StopAsync() drains in-flight handshakes
```

### Discover peers — `Discv5Discovery.StartMainnet(localKey, enqueueEnode)`

The one-line quickstart. It builds the discv5 listener, signs a local ENR, seeds the mainnet bootnodes, starts the routing-table walk, and hands back **both** objects (the caller owns teardown of each).

```csharp
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;

var localKey = EthECKey.GenerateKey();

var (discovery, listener) = Discv5Discovery.StartMainnet(
    localKey,
    enqueueEnode: enode => Console.WriteLine($"discovered {enode}"));   // feed your dial loop here

// ... on shutdown — the service does NOT dispose the listener it walks:
await discovery.StopAsync();
await listener.DisposeAsync();
```

Full signature (the optional parameters tune bind address, port, logging, and a pre-dial fork-id filter):

```csharp
(Discv5PeerDiscoveryService Discovery, Discv5Listener Listener) StartMainnet(
    EthECKey localKey,
    Action<string> enqueueEnode,
    IPAddress bindAddress = null,
    int udpPort = 0,
    Action<string> log = null,
    Func<byte[], bool> ethForkIdFilter = null,
    CancellationToken ct = default)
```

**Other discovery paths:**

- **discv4** — `PeerDiscoveryService.DiscoverAsync` bonds with seed enodes and returns a flat `List<string>` of discovered enodes:

  ```csharp
  Task<List<string>> DiscoverAsync(
      IEnumerable<string> seedEnodes, TimeSpan perSeedTimeout, CancellationToken ct)
  ```

- **DNS ENR-tree** (EIP-1459) — `EnrTreeResolver.ResolveAsync` walks an `enrtree://` URL into enode URLs:

  ```csharp
  Task<List<string>> ResolveAsync(
      string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
  ```

## Installation

```bash
dotnet add package Nethereum.DevP2P
```

### Dependencies

Nethereum.RLP, Nethereum.Signer, Nethereum.Util (direct project references). The `[NethereumDocExample]` / `DocSection` doc-tagging types used to trace README examples to source are supplied by `Nethereum.Util` (`Nethereum.Documentation.NethereumDocExampleAttribute`), not by a separate `Nethereum.Documentation` project reference. IronSnappy and Microsoft.Extensions.Logging.Abstractions are package references. Uses `Nethereum.Model` (`.P2P`, `.Enr`) transitively. Targets net8.0/net9.0/net10.0.

## RLPx transport

- **`RlpxListener`** — inbound TCP acceptor. `Start(port, bindAddress)`, `StopAsync()`; raises `PeerAccepted` (`RlpxConnection`) and `PeerFailed`. Enforces admission control before and after the handshake (see below).
- **`RlpxConnection`** — one encrypted, framed connection (inbound or outbound). Inbound `AcceptIncomingAsync(tcpClient, ct)`. Two mutually-exclusive client modes: pull-based (`ReceiveMessageAsync` / `RequestAsync`) or request-id-multiplexed (`SendRequestAsync`, which starts a dispatcher and routes unsolicited gossip to a bounded `PushMessageReceived` channel). Exposes `SharedCapabilities`, `RemoteNodeId`, `DisconnectAsync(reason)`. Outbound dial signature (note the third parameter is `remotePubNoPrefix` — the 64-byte uncompressed public key without the `0x04` prefix, not a prefixed key):

  ```csharp
  Task ConnectAsync(string host, int port, byte[] remotePubNoPrefix, CancellationToken ct = default)
  ```
- **`RlpxHandshake`** — the ECIES auth/ack exchange producing `RlpxSecrets`; **`RlpxFrameWriter`/`RlpxFrameReader`** do 32-byte-header + Keccak-MAC framing.
- **`CapabilityNegotiator.Negotiate`** — computes the shared capability set and each protocol's message-id block (base `0x10`, version-aware slot counts: `eth` 17 (v68) / 18 (v69–70) / 20 (v71), `snap` 8 (v1) / 10 (v2)). Getting the `eth` length wrong shifts `snap`'s base and breaks the peer's dispatcher — hence the dedicated negotiator.

  ```csharp
  List<P2PCapability> Negotiate(List<P2PCapability> local, List<P2PCapability> remote)
  ```
- **`StaticPeerConnector`** — parse an `enode://` URL and dial it in one call.
- **`EnodeUrl`** — the `enode://` parser/formatter primitive. `Parse` splits pubkey/host/ports (including the `?discport=` query); the result exposes `PublicKey`, `Host`, `Port`, `DiscoveryPort`, and `PeerId` (the pubkey hex).

  ```csharp
  EnodeUrl Parse(string enode)
  ```
- **Outbound dial errors** — `RlpxConnection.ConnectAsync` throws **`RlpxPeerRejectedException`** (carries the peer's `DisconnectReason`) when the remote rejects the handshake, and **`RlpxNetRestrictedException`** (`Host`, `ResolvedAddresses`) when none of the target's resolved IPs match the configured `NetRestrict` allow-list.

`RlpxConnection` advertises `eth/68`, `eth/69`, `eth/70`, `eth/71`, `snap/1` in its Hello (and `snap/2` behind `DevP2PConfig.AdvertiseSnap2`).

## Discovery

- **`Discv4Listener`** — signed unencrypted UDP discovery with discv4 endpoint-proof bonding and amplification defence (back-ping throttle). Kademlia `Discv4RoutingTable`. **`PeerDiscoveryService`** is the high-level harvester: `DiscoverAsync(seedEnodes, perSeedTimeout, ct)`.
- **`Discv5Listener`** — authenticated discovery (WHOAREYOU + HKDF session keys, AES-GCM, opaque TalkReq/TalkResp). Per-IP `TokenBucketRateLimiter` + banned-IP LRU *before* any crypto. **`Discv5PeerDiscoveryService`** runs the periodic FindNode walk; **`Discv5Bootnodes.ResolveMainnet()`** seeds it.
- **`EnrTreeResolver`** — EIP-1459 DNS ENR-tree discovery (`ResolveAsync` / `ResolveEnrsAsync`) with OS-DNS-then-public-resolver fallback; knows the mainnet all/snap/les trees.

Node identity: enode is the 64-byte secp256k1 public key (discv4 id); the discv5 node id is `keccak256(pubkey)`. ENR records (`EnrRecord`, from `Nethereum.Model.Enr`) are set on listeners via `LocalEnrEncoded` / `LocalEnrSequence`.

## Peering & DoS hardening

- **`DialScheduler`** — gates outbound dials: bounded concurrency, an inbound/outbound ratio (`MaxPeers/2 + 1` outbound cap), recent-dial suppression, and a trusted bypass. `TryReserveSlotAsync(candidate, ct)`, `OnPeerConnected` / `OnPeerDisconnected`. The `candidate` is a **`DialCandidate`** (`new DialCandidate(string key, bool isTrusted = false)` — a stable per-peer key plus its trusted flag); `OnPeerConnected` / `OnPeerDisconnected` take a **`PeerDirection`** (`Inbound` / `Outbound`) to maintain the ratio counters.
- **Inbound admission** (layered, cheapest first, in `RlpxListener` via `RlpxInboundAdmission`): `NetRestrict` CIDR allow-list → per-IP cap (`MaxInboundPerIP`) → per-subnet cap (`MaxInboundPerSubnet`) → post-handshake `MaxPeers` with a `TrustedNodeIds` bypass. Rejections raise the `PeerFailed` event carrying a `Phase` tag (`InboundNetRestrict` / `InboundPerIPCap` / `InboundPerSubnetCap` / `MaxPeers`). `SubnetTracker` is the sibling per-subnet eclipse cap for *outbound* dial diversity in the peer pool (it exempts loopback; the inbound gate does not). Both share the `ConcurrentCounter` reserve/release primitives.
- **`PersistentPeerCache`** — records dial success/failure and returns preferred enodes across restarts.

## Configuration — `DevP2PConfig`

Key knobs: `NetworkId`, `GenesisHash`, `ForkBlockNumbers` / `ForkTimestamps`, `StaticPeers`, `TrustedNodeIds`, `NetRestrict`, `MaxPeers` (25), `MaxInboundPerIP` (9), `MaxInboundPerSubnet` (18), `DialScheduler` (`DialSchedulerOptions`), and timeouts (`ConnectTimeoutMs`, `HandshakeTimeoutMs`, `RequestTimeoutMs`, `ReadTimeoutMs`, `PingIntervalMs`). `DevP2PConfig.ForDevChain(genesisHash, networkId)` is a convenience factory.

## Usage examples

**Two-node RLPx handshake:**

```csharp
var listener = new RlpxListener(serverKey, config);
listener.PeerAccepted += (_, conn) => { /* conn.SharedCapabilities negotiated */ };
listener.Start(port: 0, bindAddress: IPAddress.Loopback);

var client = new RlpxConnection(clientKey, config);
await client.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
// client.SharedCapabilities contains eth >= 68
```

**discv4 ping/pong:**

```csharp
var routing = new Discv4RoutingTable(serverKey.GetPubKeyNoPrefix());
using var server = new Discv4Listener(serverKey, routing);   // AutoRespond = true by default
server.Start(udpPort: 0, bindAddress: IPAddress.Loopback);
```

**Serving snap/1 over a DevP2P listener** (handlers come from Nethereum.DevP2P.Sync): start an `RlpxListener`, and on `PeerAccepted` perform the eth Status exchange, then dispatch snap messages to `PatriciaSnapRequestHandler`. See `Devp2pSnapTestConformanceTests` in the integration tests.

## Notes

- The eth/snap **protocol handlers** are in Nethereum.DevP2P.Sync; the wire **messages** are in Nethereum.Model.P2P. This package is transport + discovery only.
- Inbound connections are admission-controlled at several layers before they cost CPU; discovery has its own pre-crypto rate limiting.
