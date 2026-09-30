---
name: devp2p-peer-discovery
description: Help users discover Ethereum peer addresses over devp2p when they don't have an enode:// URL yet — discv4 Kademlia UDP discovery, discv5 authenticated discovery, DNS ENR-tree bootstrapping (EIP-1459), or persisting a stable node identity/peer cache with Nethereum (.NET). Use this skill whenever the user mentions peer discovery, discv4, discv5, ENR, enode, finding Ethereum nodes, bootnodes, DNS discovery tree, or persisting a node key/peer list across restarts.
user-invocable: true
---

# Peer Discovery — Nethereum.DevP2P

Dialing a peer (see `devp2p-peer-connect`) assumes you already have an `enode://` URL. In practice, a node starting cold has none — it needs to find other nodes on the network before it can dial anyone. This skill covers the three discovery mechanisms Nethereum implements — **discv4** (the original Kademlia UDP protocol), **discv5** (its authenticated successor), and **DNS ENR-tree lookup** (EIP-1459, for bootstrapping from a small number of well-known DNS names) — plus two supporting pieces every long-running node needs: a stable node identity and a memory of which peers were worth reconnecting to.

## Package

```bash
dotnet add package Nethereum.DevP2P
```

You'll want a persisted node identity (below) rather than a throwaway `EthECKey.GenerateKey()` — discovery is precisely the mechanism by which *other* nodes learn your `enode://` and remember you, so a node-id that changes every restart defeats the point.

## Persist your node identity

A stable enode across restarts is what lets a remote peer durably trust your node by node-id — bond once, reconnect for weeks. `NodeKeyStore.LoadOrCreate` loads a raw 32-byte private key from `path`, or generates and saves one on first run:

```csharp
using Nethereum.DevP2P.NodeDb;

// EthECKey LoadOrCreate(string path, Action<string> log = null)
var localKey = NodeKeyStore.LoadOrCreate("nodekey");
```

Run this once at node startup instead of `EthECKey.GenerateKey()`. The first run creates `nodekey` on disk; every subsequent run loads the same key, so your node's identity (and any reputation peers have built for it) survives restarts.

## Mental model: three ways to find a node

| Mechanism | Transport | Use when |
|---|---|---|
| **discv4** | Unencrypted, signed UDP (Kademlia) | Talking to older/simpler clients; bonding with a known seed list |
| **discv5** | Authenticated UDP (WHOAREYOU + HKDF session keys, AES-GCM) | Mainnet-style discovery; the modern default with TalkReq/TalkResp extensibility |
| **DNS ENR-tree (EIP-1459)** | DNS TXT records | Bootstrapping from a small number of trusted, well-known hostnames rather than hardcoded IPs |

All three ultimately hand you the same thing: a list of enode (or ENR) addresses to feed into `StaticPeerConnector`/`RlpxConnection` (see `devp2p-peer-connect`). They differ in how much trust and infrastructure they assume up front.

## Discover peers with discv5 (the one-line quickstart)

`Discv5Discovery.StartMainnet` is the fastest way to get a discv5 node walking the network: it builds the listener, signs a local ENR, seeds the mainnet bootnodes, starts the routing-table walk, and hands back both objects — you own teardown of each:

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

Every enode string handed to `enqueueEnode` is a live candidate — pass it straight into `StaticPeerConnector.ConnectAsync`, or queue it for your own dial scheduler. Note the two-step shutdown: `Discv5Discovery` stops the periodic walk, but it doesn't own the listener's socket, so you dispose that separately.

The full signature exposes tuning most callers don't need day one — a custom bind address/port, a logger, and a pre-dial fork-ID filter so you never bother dialing peers on the wrong chain:

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

## See the underlying protocol: discv4 ping/pong

`Discv5Discovery` is a convenience wrapper; underneath, discovery is a signed UDP request/response protocol. This is easiest to see with the older discv4 protocol, whose ping/pong bonding is exercised directly by a test — two `Discv4Listener`s on loopback, each auto-responding to the other's messages:

```csharp
var keyA = EthECKey.GenerateKey();
var keyB = EthECKey.GenerateKey();

var tableA = new Discv4RoutingTable(keyA.GetPubKeyNoPrefix());
var tableB = new Discv4RoutingTable(keyB.GetPubKeyNoPrefix());

using var nodeA = new Discv4Listener(keyA, tableA);
using var nodeB = new Discv4Listener(keyB, tableB);

nodeA.Start(udpPort: 0, bindAddress: IPAddress.Loopback);
nodeB.Start(udpPort: 0, bindAddress: IPAddress.Loopback);

nodeB.PingReceived += async (_, e) =>
{
    var pong = new Discv4PongMessage
    {
        To = new Discv4Endpoint { IP = e.Sender.Address, UdpPort = (ushort)e.Sender.Port, TcpPort = 0 },
        PingHash = e.PingHash,
        Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
    };
    await nodeB.SendPongAsync(e.Sender, pong);
};

var ping = new Discv4PingMessage
{
    Version = 4,
    From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeA.Port, TcpPort = 30303 },
    To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)nodeB.Port, TcpPort = 0 },
    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
};
await nodeA.SendPingAsync(new IPEndPoint(IPAddress.Loopback, nodeB.Port), ping);

// After the round trip, both routing tables have learned about each other:
// tableA.Count >= 1 and tableB.Count >= 1
```

This is the EIP-868 endpoint-proof bonding step: a node only adds a peer to its Kademlia routing table after a successful ping/pong round trip, which is what makes discv4 resistant to naive amplification/spoofing attacks. `Discv4Listener` has `AutoRespond = true` by default, so in practice you rarely wire the `PingReceived` handler yourself — the harvester below does it for you.

## The high-level discv4 harvester

For day-to-day use you don't drive ping/pong by hand — `PeerDiscoveryService.DiscoverAsync` bonds with a list of seed enodes and returns the flat list of enodes it discovered:

```csharp
Task<List<string>> DiscoverAsync(
    IEnumerable<string> seedEnodes, TimeSpan perSeedTimeout, CancellationToken ct)
```

Feed it your bootnodes (or a previously-discovered peer list — see the persistent cache below) and it returns candidates ready for `StaticPeerConnector`.

> **Note:** this signature is verified against `src/Nethereum.DevP2P/README.md`, but no tagged doc-example test exercises the harvester end-to-end — only the lower-level `Discv4Listener` ping/pong above is test-backed.

## DNS ENR-tree discovery (EIP-1459)

Some networks publish their bootstrap peer list as a DNS TXT-record tree rather than a hardcoded list — this is how go-ethereum distributes its mainnet bootnodes, for example. `EnrTreeResolver.ResolveAsync` walks an `enrtree://` URL into a flat list of enode URLs:

```csharp
Task<List<string>> ResolveAsync(
    string enrtreeUrl, TimeSpan timeout, int maxLeaves, CancellationToken ct)
```

`EnrTreeResolver` falls back from OS DNS to a public resolver if needed, and Nethereum ships the mainnet all/snap/les tree URLs baked in, so you rarely need to source one yourself. Use this when you want to bootstrap a node with zero hardcoded IPs — just the tree's root name.

## Node identity concepts

Two different identifiers are in play across discv4 and discv5, and mixing them up is a common source of confusion:

- **enode id** (discv4) — the raw 64-byte secp256k1 public key.
- **discv5 node id** — `keccak256(pubkey)`, a further hash of that same key.

ENR records (`EnrRecord`, from `Nethereum.Model.Enr`) carry a node's discv5 identity and are set on a listener via `LocalEnrEncoded` / `LocalEnrSequence`.

## Remember which peers were worth reconnecting to

Discovery finds candidates; it doesn't know which of them turned out to be reliable. `PersistentPeerCache` records dial success/failure per peer and returns your preferred enodes across restarts, so a node doesn't have to rediscover its whole working peer set from scratch every time it starts.

> **Note:** `PersistentPeerCache` is documented in `src/Nethereum.DevP2P/README.md` as a bullet point (recording dial success/failure, returning preferred enodes) but has no method signature block in the README and no tagged doc-example test — its role is verified from the README prose only.

## Common mistakes

| Symptom | Cause | Fix |
|---|---|---|
| `Discv5Discovery.StartMainnet` never calls `enqueueEnode` | Bound to an address/port that's unreachable from the internet (e.g. behind strict NAT with no port forwarding), or outbound UDP blocked | Confirm the UDP port is reachable; check firewall/NAT |
| discv4 `PingReceived` never fires in your own listener code | `AutoRespond` already handled it — you don't usually need to also handle `PingReceived` yourself unless customizing bonding | Only hook `PingReceived`/`PongReceived` for diagnostics or custom logic |
| Node's peer reputation resets every deploy | Using `EthECKey.GenerateKey()` at startup instead of `NodeKeyStore.LoadOrCreate` | Persist the node key; see "Persist your node identity" above |
| DNS ENR-tree resolution times out | `timeout`/`maxLeaves` too small for the tree's depth, or OS DNS is filtering `TXT` records | Increase `timeout`; `EnrTreeResolver` falls back to a public resolver automatically |

## Decision guidance

| I want to… | Use |
|---|---|
| Get discovering with the least setup (mainnet-style) | `Discv5Discovery.StartMainnet` |
| Match an older/simpler peer set, or need fine control over bonding | `Discv4Listener` + `PeerDiscoveryService.DiscoverAsync` |
| Bootstrap with zero hardcoded IPs | `EnrTreeResolver.ResolveAsync` against a known `enrtree://` root |
| Keep working peers across restarts | `PersistentPeerCache` |
| Keep the same node-id across restarts | `NodeKeyStore.LoadOrCreate` |

Once you have candidate enodes, dial them with `StaticPeerConnector.ConnectAsync` — see the `devp2p-peer-connect` skill.

For full documentation, see: https://docs.nethereum.com/docs/devp2p/guide-peer-discovery
