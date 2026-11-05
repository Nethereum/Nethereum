# Nethereum.DevP2P

Ethereum peer-to-peer transport and node discovery: RLPx framing + handshake, discovery (discv4, discv5, DNS ENR trees), and peer dial scheduling.

## Overview

Nethereum.DevP2P is the **transport and discovery** layer of a Nethereum node. It implements:

- **RLPx** — the encrypted TCP transport: ECIES handshake, AES-CTR + Keccak-MAC framing, and capability multiplexing (many sub-protocols over one socket).
- **Node discovery** — UDP **discv4** (Kademlia, signed) and **discv5** (authenticated WHOAREYOU/HKDF sessions with TalkReq/TalkResp), plus **DNS ENR-tree** discovery and hardcoded bootnodes.
- **Peering** — outbound dial scheduling with inbound/outbound ratio control, and layered inbound DoS hardening.

This package advertises `eth/68` up to `eth/71` (capped by `DevP2PConfig.MaxEthVersion`, 71 by default) and `snap/1` (unless `DevP2PConfig.AdvertiseSnap1` is `false`), plus `snap/2` when `DevP2PConfig.AdvertiseSnap2` is set, and computes their message-id offsets, but it **does not contain the eth/snap protocol handlers or message classes**. The sub-protocol serving/sync logic lives in **Nethereum.DevP2P.Sync** (e.g. `PatriciaSnapRequestHandler`, `Eth68ServerSession`, `SnapSyncClient`, `SyncPeerSession`, `IEthPeer`), and the wire-message models live in **Nethereum.Model** (`Nethereum.Model.P2P`, with the snap/1 and snap/2 messages nested under `Nethereum.Model.P2P.Snap`). Think of this package as the pipe; the protocols that flow through it are layered on top.

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

For a **stable enode across restarts** — required for a remote peer to durably trust your node by node-id — persist the node key instead of generating a fresh one each run. `NodeKeyStore.LoadOrCreate` loads a raw 32-byte private key from `path`, or generates and saves one on first run (on Unix the file is created with mode `0600` from its first byte):

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
- **`StaticPeerConnector`** — parse an `enode://` URL and dial it in one call; a dial that fails disposes its socket at once.
- **`EnodeUrl`** — the `enode://` parser/formatter primitive. `Parse` splits pubkey/host/ports (including the `?discport=` query) and throws for a malformed enode, `TryParse(enode, out parsed)` returns `false` instead (including for a public key that is not hex); the result exposes `PublicKey`, `Host`, `Port`, `DiscoveryPort`, and `PeerId` (the pubkey hex).

  ```csharp
  EnodeUrl Parse(string enode)
  ```
- **Outbound dial errors** — `RlpxConnection.ConnectAsync` throws **`RlpxPeerRejectedException`** (carries the peer's `DisconnectReason`) when the remote rejects the handshake, and **`RlpxNetRestrictedException`** (`Host`, `ResolvedAddresses`) when none of the target's resolved IPs match the configured `NetRestrict` allow-list.

`RlpxConnection` advertises `eth/68` to `eth/71` (up to `DevP2PConfig.MaxEthVersion`) and `snap/1` (when `DevP2PConfig.AdvertiseSnap1`) in its Hello, and `snap/2` behind `DevP2PConfig.AdvertiseSnap2`. `DevP2PConfig.WithMaxEthVersion(maxEthVersion)` returns a copy of the config with a lower cap:

```csharp
DevP2PConfig WithMaxEthVersion(int maxEthVersion)
```

A capability is shared only when its name and version appear in both Hello lists, and the highest such version is used; a peer that offers only versions this node does not advertise shares nothing for that protocol.

### Message limits, deadlines and decode back-pressure

- **Frames.** A frame is at most `RlpxFrameReader.MaxFrameSize` (16 MiB - 1) once Snappy-decoded (`MaxDecompressedFrameSize` is the same limit). Base-protocol messages (ids below `0x10`) are capped at `RlpxFrameReader.BaseProtocolMaxMsgSize` (2048 bytes), and a frame larger than that plus 16 bytes is refused until the Hello is complete; a zero-size frame raises `CryptographicException`. The Hello must carry the 64-byte node id the handshake authenticated (`NullIdentity` / `UnexpectedIdentity` otherwise), at most 64 capabilities and a client id of at most 256 characters, and a peer that shares no capability is refused with `UselessPeer`.
- **`eth` and `snap` messages: 10 MiB.** A message whose payload exceeds `RlpxConnection.MaxSubProtocolMessageSize` (10 * 1024 * 1024 bytes) is refused with `DisconnectReason.ProtocolBreach` before any decode. The cap applies on both read paths: the request-id dispatcher loop (`SendRequestAsync`) and the pull-based `ReceiveMessageAsync` / `ReceiveStatusAsync`. A message id outside every negotiated capability is a `ProtocolBreach` on both paths too, before it counts as liveness. Payloads of other capabilities are bound by the frame size only.
- **Bounded responses.** `SendRequestAsync` takes an `RlpxResponseBound` and the dispatcher checks every response against it before decoding. `RlpxResponseBound.Items(maxItems)` bounds the number of items in the response list, `RlpxResponseBound.ContentBytes(maxBytes)` bounds the list's content length, `RlpxResponseBound.Unbounded` (the default) bounds neither; `Kind` (`RlpxResponseBoundKind`) and `Limit` expose the choice. A response with an unknown request id, a message id other than the one the request expects, a malformed envelope, or more items or bytes than the bound fails the request and disconnects the peer with `ProtocolBreach`; a response that arrives after its request timed out is dropped quietly for `DevP2PConfig.AbandonedRequestTrackingMs` (5 minutes). `RlpxResponseCodes.IsResponse(capabilityName, relativeMessageId)` tells which `eth` and `snap` message ids are responses.

  ```csharp
  Task<byte[]> SendRequestAsync(
      int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
      TimeSpan timeout, RlpxResponseBound bound, CancellationToken ct = default)
  ```

- **Strict canonical RLP.** Response envelopes, and the wire messages decoded in `Nethereum.Model.P2P`, are read with `Nethereum.RLP.RlpReader`, which refuses what a canonical encoder never produces: a single byte below `0x80` carrying a string prefix, a length-of-length with a leading zero, a long form used for a payload under 56 bytes, an integer with a leading zero or wider than 64 bits, nesting deeper than `RLP.MaxDecodeDepth` (64), and bytes after the top-level value. A malformed response envelope is a `ProtocolBreach`.
- **Decode back-pressure.** `InFlightDecodeLimiter` caps the bytes being decoded across every connection that shares it. `AcquireAsync(bytes, ct)` returns an `InFlightDecodeLease` that is released by `Dispose()`; waiters are admitted in arrival order, a request larger than the capacity is admitted when nothing else is in flight, and the read loop waits for a lease instead of buffering. `InFlightDecodeLimiter.Shared` holds `DefaultMaxPeers` (25) x `BytesPerPeer` (4 MiB) = 100 MiB, `InFlightDecodeLimiter.ForMaxPeers(maxPeers)` sizes one for a different peer count, `CapacityBytes` and `InFlightBytes` report its state. `DevP2PConfig.DecodeLimiter` selects the limiter a connection uses (`Shared` when `null`), and `RlpxConnection.AcquireDecodeLeaseAsync(payloadBytes, ct)` lets a serving loop lease before it decodes.

  ```csharp
  var limiter = InFlightDecodeLimiter.ForMaxPeers(25);
  var config = DevP2PConfig.ForDevChain(genesisHash);
  config.DecodeLimiter = limiter;

  using (var lease = await limiter.AcquireAsync(payload.Length))
  {
      // decode the payload while the lease is held
  }
  ```

- **Deadlines.** `HandshakeTimeoutMs` (5 s) bounds the RLPx handshake. `RlpxConnection.ReceiveStatusAsync` bounds the eth Status exchange by `StatusHandshakeTimeoutMs` (5 s) for the whole call, so a peer that only pings never holds a slot. `ReceiveMessageAsync(frameReadTimeout, ct)` arms a fresh deadline for every frame, ping and pong included (`FrameReadTimeout` is `ReadTimeoutMs`, 30 s). Every frame write has a `FrameWriteTimeoutMs` (20 s) deadline; a write that exceeds it, or one the caller cancels after the frame began, disconnects and disposes the connection.
- **Peer slots.** `RlpxPeerSlots(maxPeers)` hands out one slot per admitted peer: `TryAcquire(trusted)` refuses an untrusted peer once `Active` reaches `maxPeers` and never refuses a trusted one, `Release()` frees a slot and ignores an unbalanced call. `RlpxListener` claims its `MaxPeers` slots through it, so concurrent handshakes cannot overshoot.

  ```csharp
  var slots = new RlpxPeerSlots(maxPeers: 3);
  slots.TryAcquire(trusted: false);
  slots.TryAcquire(trusted: true);
  slots.Release();
  ```

## Discovery

- **`Discv4Listener`** — signed unencrypted UDP discovery with discv4 endpoint-proof bonding and amplification defence: a sender enters the routing table only after it answers a PING with a PONG, and FINDNODE and ENRRequest are answered only after an endpoint proof from the same IP. An unbonded sender is pinged back at once, with at most `MaxPendingPings` (1024) back-pings outstanding; a bond lasts 12 hours and at most `MaxBondedPeers` (8192) are kept, evicting table members first (revalidation renews their proof), then peers outside the table newest first, never the incoming bond. `RevalidateAsync` pings the least recently seen table entry every `RevalidationInterval` (10 s) and evicts it when no PONG arrives within `RevalidationTimeout` (2 s). NEIGHBORS replies are chunked at `Discv4NeighborsMessage.MaxNeighbors` (12) so each fits one datagram, and only NEIGHBORS answering a FINDNODE this node sent are harvested. `SendPingAsync` and `SendFindNodeAsync` take the optional `remoteNodeId` the reply must come from; expirations are read as unsigned 64-bit (`Discv4MessageEncoder.DecodeEnrRequestExpiration`). Kademlia `Discv4RoutingTable` (`BucketSize` 16) admits at most `BucketIpLimit` (2) nodes per bucket and `TableIpLimit` (10) per table from one /`IpLimitSubnetBits` (24) subnet and keeps up to `MaxReplacements` (10) replacements per bucket (`ReplacementsFor`, `NextRevalidationCandidate`). **`PeerDiscoveryService`** is the high-level harvester: `DiscoverAsync(seedEnodes, perSeedTimeout, ct)`; it skips nodes on special-purpose networks, relayed nodes whose address the sender could not legitimately name (`IpAddressExtensions.IsValidRelayTarget`) and nodes on UDP ports of 1024 or below.
- **`Discv5Listener`** — authenticated discovery (WHOAREYOU + HKDF session keys, AES-GCM, opaque TalkReq/TalkResp). Every ENR in a NODES reply is signature-verified before it reaches the routing table or the dial queue, and a NODES reply is accepted only for a FINDNODE this node has pending (`Discv5RequestTracker`: at most `MaxNodesEnrsPerRequest` 64 records in `MaxNodesChunksPerRequest` 16 chunks, each record checked for the requested distance, duplicates, a legitimate relay address and a UDP port above 1024). FINDNODE distances are bounded by `MaxLogDistance` (256) and de-duplicated, at most `MaxConcurrentTalkRequests` (1024) TalkReq are served at once, and a failed handshake consumes its WHOAREYOU challenge on every error path. Inbound bans expire after `BanDuration` (`DefaultBanDuration`, 5 minutes). The per-IP `TokenBucketRateLimiter` + banned-IP LRU that runs *before* any crypto is opt-in through `EnableInboundPacketFilter` (off by default, so several nodes behind one address can handshake). `Discv5RoutingTable` honours the ENR sequence number, applies the same per-bucket and per-table IP limits as the discv4 table, keeps `MaxReplacements` (10) replacements per bucket (`ReplacementsAtDistance`), and `Remove`s an entry after `MaxFailures` (3) consecutive walk failures (`RecordFailure`, `RecordSuccess`). `Discv5Listener.SessionManager` exposes the `Discv5SessionManager`. **`Discv5PeerDiscoveryService`** runs the periodic FindNode walk; **`Discv5Bootnodes.ResolveMainnet()`** seeds it.
- **`EnrTreeResolver`** — EIP-1459 DNS ENR-tree discovery (`ResolveAsync` / `ResolveEnrsAsync`) with OS-DNS-then-public-resolver fallback; knows the mainnet all/snap/les trees. Each child entry is verified against its label hash, ENR signatures are verified, the root signature must be low-S and a root with a lower sequence number than one already seen is ignored. The walk is bounded to `MaxTreeDepth` (32) levels and by a query budget, a malformed link does not abort the resolution, and `EnrTreeResolver(Action<string> log, Func<string, TimeSpan, CancellationToken, Task<List<string>>> queryTxt)` supplies the TXT lookup.

Node identity: enode is the 64-byte secp256k1 public key (discv4 id); the discv5 node id is `keccak256(pubkey)`. ENR records (`EnrRecord`, from `Nethereum.Model.Enr`) are set on listeners via `LocalEnrEncoded` / `LocalEnrSequence`.

## Peering & DoS hardening

- **`DialScheduler`** — gates outbound dials: bounded concurrency, an inbound/outbound ratio (`MaxPeers/2 + 1` outbound cap), recent-dial suppression, and a trusted bypass. `TryReserveSlotAsync(candidate, ct)` claims the candidate's dial-history entry atomically and releases it again when no slot is granted, `OnPeerConnected` / `OnPeerDisconnected`. The `candidate` is a **`DialCandidate`** (`new DialCandidate(string key, bool isTrusted = false)` — a stable per-peer key plus its trusted flag); `OnPeerConnected` / `OnPeerDisconnected` take a **`PeerDirection`** (`Inbound` / `Outbound`) to maintain the ratio counters.
- **Inbound admission** (layered, cheapest first, in `RlpxListener` via `RlpxInboundAdmission`): global pending-handshake cap (`MaxPendingPeers`, 50 by default, acquired before accepting a socket so no ECIES work runs beyond it) → `NetRestrict` CIDR allow-list → per-IP cap (`MaxInboundPerIP`, not applied to loopback, private and link-local addresses; `DevP2PConfig.IsLanAddress` is the classifier and defaults to `IpAddressExtensions.IsLan`) → per-subnet cap (`MaxInboundPerSubnet`), both held for the life of the connection → post-handshake `MaxPeers` with a `TrustedNodeIds` bypass. Rejections raise the `PeerFailed` event carrying a `Phase` tag (`InboundNetRestrict` / `InboundPerIPCap` / `InboundPerSubnetCap` / `MaxPeers`). `SubnetTracker` is the sibling per-subnet eclipse cap for *outbound* dial diversity in the peer pool (it exempts loopback; the inbound per-subnet cap does not). Both share the `ConcurrentCounter` reserve/release primitives.
- **`PersistentPeerCache`** — records dial success/failure and returns preferred enodes across restarts.

## Configuration — `DevP2PConfig`

Key knobs: `NetworkId`, `GenesisHash`, `ForkBlockNumbers` / `ForkTimestamps`, `StaticPeers`, `TrustedNodeIds`, `NetRestrict`, `MaxPeers` (25), `MaxInboundPerIP` (9), `MaxInboundPerSubnet` (18), `MaxPendingPeers` (50), `DialScheduler` (`DialSchedulerOptions`), `IsLanAddress`, `DecodeLimiter`, the capability switches `MaxEthVersion` (71), `AdvertiseSnap1` (`true`) and `AdvertiseSnap2` (`false`), and timeouts in milliseconds (`ConnectTimeoutMs` 10000, `HandshakeTimeoutMs` 5000, `StatusHandshakeTimeoutMs` 5000, `RequestTimeoutMs` 5000, `ReadTimeoutMs` 30000, `FrameWriteTimeoutMs` 20000, `AbandonedRequestTrackingMs` 300000, `PingIntervalMs` 15000). `DevP2PConfig.ForDevChain(genesisHash, networkId)` is a convenience factory.

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
- Inbound connections are admission-controlled at several layers before they cost CPU; discovery can opt in to its own pre-crypto rate limiting.
