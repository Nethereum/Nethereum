---
name: devp2p-peer-connect
description: Help users connect to an Ethereum peer over devp2p, dial a node by its enode:// URL, accept inbound RLPx connections, or understand devp2p handshake and capability negotiation with Nethereum (.NET). Use this skill whenever the user mentions connecting to an Ethereum peer, RLPx handshake, enode URL, StaticPeerConnector, RlpxListener, ECIES key exchange, capability negotiation (Hello message, eth/snap sub-protocols), or fork-ID compatibility when peering with go-ethereum/geth or another client.
user-invocable: true
---

# Connect to a Peer — Nethereum.DevP2P

Before a Nethereum node can sync blocks, serve state, or discover the rest of the network, it needs to open at least one encrypted connection to another Ethereum node. This skill covers the two ways a connection starts — dialing a peer whose `enode://` address you already have, and accepting inbound connections from peers who dial you — plus what happens right after the socket opens: capability negotiation, which decides which sub-protocols (`eth`, `snap`) the two nodes will actually speak to each other.

Use this skill for the transport layer only. For finding `enode://` addresses when you don't have one, use the `devp2p-peer-discovery` skill. For wiring connecting + serving + syncing together into one node object, use `devp2p-run-a-node`.

## Package

```bash
dotnet add package Nethereum.DevP2P
```

## Mental model: RLPx, then capabilities

Every devp2p connection is a two-layer negotiation:

1. **RLPx handshake** — an ECIES key exchange establishes a shared secret, then the connection switches to AES-CTR + Keccak-MAC framing. Both sides then exchange a `Hello` message listing the sub-protocols (`eth/68`, `eth/69`, `snap/1`, and so on) each one supports.
2. **Capability negotiation** — the two `Hello` capability lists are intersected. Only protocols both sides advertise survive, and each surviving protocol is assigned a block of message IDs on the shared socket (multiple sub-protocols share one TCP connection).

`Nethereum.DevP2P` handles both layers and hands you back a connection with `SharedCapabilities` already resolved. It does **not** run the `eth` Status exchange or the snap protocol loop — those live one layer up, in `Nethereum.DevP2P.Sync` (`SyncPeerSession`, `PeerListener`), covered by the `devp2p-run-a-node` skill.

## Generate a node identity and config

Every example below needs a key and a `DevP2PConfig`. `DevP2PConfig.ForDevChain` collapses the common dev-chain defaults into one call:

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

A key generated with `EthECKey.GenerateKey()` is fine for scripts and tests, but a real node loses its identity — and any peer that trusted it by node-id — on every restart if you don't persist it. For a long-running node, use `NodeKeyStore.LoadOrCreate` (see `devp2p-peer-discovery`).

## Dial a peer you already have the address for

If you already have a peer's `enode://` URL — from a config file, a static-peers list, or a discovery result — `StaticPeerConnector.ConnectAsync` parses it and completes the RLPx handshake and `Hello` exchange in one call:

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

The `<64-byte-hex-pubkey>` is the peer's uncompressed secp256k1 public key without the `0x04` prefix, hex-encoded — the same identity that shows up as `PeerId` if you parse the URL with `EnodeUrl.Parse`. Once `ConnectAsync` returns, `conn.SharedCapabilities` tells you what you can actually do with this peer: if `eth` isn't in the list, this peer won't answer block/header requests; if `snap` isn't there, it won't serve state ranges.

## Accept inbound connections

To let peers dial *you*, start an `RlpxListener`. It performs admission control, the RLPx handshake, and the `Hello` exchange, then raises `PeerAccepted` once a peer is fully connected:

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

`RlpxListener` deliberately stops at "you have a negotiated connection." It does not know about `eth` Status messages or snap requests — that dispatch loop is `devp2p-run-a-node`'s `PeerListener`, built on top of this same event.

## See it end-to-end: a two-node handshake

Both sides run in the same process — a listener and a client, both on loopback:

```csharp
var serverKey = EthECKey.GenerateKey();
var clientKey = EthECKey.GenerateKey();
var config = new DevP2PConfig
{
    ClientId = "Nethereum/test",
    ConnectTimeoutMs = 5000,
    HandshakeTimeoutMs = 5000
};

var listener = new RlpxListener(serverKey, config);
RlpxConnection acceptedConnection = null;
var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
listener.PeerAccepted += (_, conn) =>
{
    acceptedConnection = conn;
    acceptedTcs.TrySetResult(conn);
};

listener.Start(port: 0, bindAddress: IPAddress.Loopback);
var listenerPort = listener.Port;

var clientConn = new RlpxConnection(clientKey, config);
await clientConn.ConnectAsync("127.0.0.1", listenerPort, serverKey.GetPubKeyNoPrefix());
var server = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

// Both sides are connected and see each other's Hello:
Console.WriteLine($"Client sees: {clientConn.RemoteHello.ClientId}, caps={string.Join(",", clientConn.SharedCapabilities.ConvertAll(c => $"{c.Name}/{c.Version}"))}");
Console.WriteLine($"Server sees: {server.RemoteHello.ClientId}, caps={string.Join(",", server.SharedCapabilities.ConvertAll(c => $"{c.Name}/{c.Version}"))}");

await clientConn.DisconnectAsync();
await listener.StopAsync();
```

The outbound `ConnectAsync` overload here takes `(host, port, remotePubNoPrefix)` — the raw connection form, distinct from `StaticPeerConnector`'s single `enode://` string — and `clientConn.SharedCapabilities` will contain `eth` at version 68 or higher, because that's the floor this package advertises. Port `0` in `Start` binds an OS-assigned ephemeral port, which is why the test reads `listener.Port` back before dialing it.

## Capability negotiation, in detail

`CapabilityNegotiator.Negotiate` is what computes `SharedCapabilities` from the two `Hello` messages. You don't normally call it directly — `RlpxConnection`/`RlpxListener` do it for you — but understanding it explains a real failure mode:

```csharp
List<P2PCapability> Negotiate(List<P2PCapability> local, List<P2PCapability> remote)
```

Each protocol gets a block of message IDs starting at `0x10`, sized by version: `eth` gets 17 slots at v68, 18 at v69–70, 20 at v71; `snap` gets 8 slots at v1, 10 at v2. If the `eth` version negotiated is wrong, `snap`'s message-id block shifts and the peer's dispatcher misroutes every snap message it receives — which is why this computation is centralized rather than inlined at each call site.

`RlpxConnection` advertises `eth/68`, `eth/69`, `eth/70`, `eth/71`, and `snap/1` in its own `Hello` — `snap/2` only appears when `DevP2PConfig.AdvertiseSnap2` is set, which matters for the snap-v2 BAL heal path (see `devp2p-bal-sync`).

## Why a peer might reject you: fork compatibility

Capability negotiation only checks *which protocols* two peers share — it says nothing about whether they're on the *same chain*. That check happens one layer up, in the `eth` Status message that `Nethereum.DevP2P.Sync`'s `SyncPeerSession` sends immediately after the handshake: Status carries an EIP-2124 fork-ID, and `Eip2124ForkIdCalculator`/`Eip2124ValidationResult` decide whether the two nodes are running compatible or stale fork schedules. A connection can complete everything in this skill successfully and still be useless for syncing if the fork-ID check fails afterward — see `devp2p-run-a-node`.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `ConnectAsync` throws `RlpxPeerRejectedException` | The remote peer rejected the handshake (full, banned you, or a capability mismatch) | Inspect the exception's `DisconnectReason`; not always retryable |
| `ConnectAsync` throws `RlpxNetRestrictedException` | None of the target's resolved IPs match your configured `NetRestrict` allow-list | Widen `NetRestrict`, or confirm you dialed the address you intended |
| `SharedCapabilities` is empty or missing `eth` | Passed the wrong pubkey form, or the peer only advertises protocols you don't support | Confirm you passed `remotePubNoPrefix` (64-byte uncompressed key, no `0x04`) to the raw `ConnectAsync(host, port, pubkey)` overload — `StaticPeerConnector` handles this for you from an `enode://` string |
| Connects, but never receives blocks | Handshake succeeded but the fork-ID Status check (in `Nethereum.DevP2P.Sync`) rejected the peer, or no `eth`/`snap` dispatch loop is wired to `PeerAccepted` | Use `devp2p-run-a-node` — `RlpxListener` alone does not run the protocol loop |

## Decision guidance

| I want to… | Use |
|---|---|
| Connect to one specific peer I already have an `enode://` for | `StaticPeerConnector.ConnectAsync` |
| Accept connections from peers dialing me | `RlpxListener` + `PeerAccepted` |
| Both sides in the same process (tests, local dev networks) | `RlpxListener` + `RlpxConnection.ConnectAsync(host, port, pubkey)`, as in the two-node example above |
| I don't have any peer addresses yet | Use the `devp2p-peer-discovery` skill first |

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-connect-to-a-peer
