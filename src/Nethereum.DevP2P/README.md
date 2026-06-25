# Nethereum.DevP2P

Ethereum peer-to-peer transport: RLPx framing + handshake, node discovery (discv4, discv5), and the eth and snap wire protocols.

## Overview

Nethereum.DevP2P implements Ethereum's peer-to-peer stack from the wire up. It provides the encrypted RLPx transport that every execution-layer client uses to talk to other peers, the UDP node-discovery layers (discv4 and discv5) used to bootstrap peer-to-peer connectivity, and the eth and snap sub-protocols carried over RLPx for block sync, transaction propagation and state synchronisation.

The library is the foundation underneath `Nethereum.DevP2P.Sync` and lets .NET applications participate as full Ethereum peers — listening for inbound connections, dialling out, exchanging blocks, headers, receipts and pooled transactions, and serving snap-sync state requests. The implementation is validated end-to-end against go-ethereum's `cmd/devp2p` conformance test suite (see `tests/Nethereum.DevP2P.IntegrationTests/README.md` for the test harness).

### Key Features

- **RLPx transport** — ECIES handshake, AES-CTR + Keccak-MAC framing, Snappy compression, capability negotiation
- **discv4 node discovery** — Kademlia routing, EIP-868 ENR exchange, amplification-defence on FINDNODE
- **discv5 node discovery** — encrypted session layer (WHOAREYOU + handshake + HKDF), TalkReq/TalkResp, distance-walking FINDNODE
- **eth wire protocol** — eth/68 and eth/69 (Status, GetBlockHeaders/Bodies/Receipts, NewPooledTxHashes, Pooled transactions, BlockRangeUpdate)
- **snap/1 wire protocol** — AccountRange, StorageRanges, ByteCodes, TrieNodes with edge-proof generation
- **EIP-778 ENR records** — sign and parse, with secp256k1 v4 identity scheme

## Installation

```bash
dotnet add package Nethereum.DevP2P
```

### Dependencies

- **IronSnappy** — Snappy compression of RLPx message frames after the Hello exchange
- **Microsoft.Extensions.Logging.Abstractions** — structured logging hooks
- **Nethereum.RLP** — RLP encoding for every wire message
- **Nethereum.Signer** — secp256k1 signing and key recovery for ECIES, ENR signatures, id-signatures
- **Nethereum.Util** — Keccak, byte primitives, EvmUInt256

## Key Concepts

### RLPx Connections

An `RlpxConnection` is an encrypted, framed TCP connection between two Ethereum peers. After the ECIES handshake derives a shared secret, both sides negotiate sub-protocol capabilities (`eth`, `snap`, `les`) and from then on every framed message carries a sub-protocol message id. The connection multiplexes all capabilities over a single TCP socket.

The `RlpxListener` accepts inbound connections; outbound dialling is handled directly by `RlpxConnection.ConnectAsync`. Both sides exchange a `HelloMessage` advertising their capabilities; from that point the connection is ready to carry sub-protocol traffic.

### Discovery — discv4 vs discv5

Discovery happens over UDP and is the only thing in the stack that's not RLPx. **discv4** is the original Kademlia-style discovery layer: it uses signed but unencrypted UDP packets and exchanges ENRs (Ethereum Node Records) on demand. **discv5** is its successor: it adds an authenticated key-exchange (the WHOAREYOU challenge + HKDF-derived session keys) so all discovery traffic after the handshake is AES-GCM encrypted, and supports an opaque TalkReq/TalkResp channel for higher-level protocols.

Both implementations maintain a Kademlia routing table (`Discv4RoutingTable` / `Discv5RoutingTable`) keyed by node-id log-distance from the local node.

### eth/68 and eth/69

eth/68 is the long-lived block-and-tx-pool protocol — it carries `GetBlockHeaders`, `GetBlockBodies`, `GetReceipts`, `NewPooledTransactionHashes`, `GetPooledTransactions`, and the canonical `Transactions` message. eth/69 layers on a richer `Status` message (with `EarliestBlock` / `LatestBlock` / `LatestBlockHash`) and the `BlockRangeUpdate` push, used for post-merge canonical-head tracking.

### snap/1

snap/1 is the modern state-sync protocol. Instead of dragging a peer through every historical receipt, a snap server serves contiguous *ranges* of accounts and storage slots from a state trie, plus the bytecodes that the served accounts reference. Each range comes with an edge proof that lets the client verify it against a trusted state root without re-running the full trie. `PatriciaSnapRequestHandler` implements the server side; `SnapSyncClient` is the client.

### EIP-778 ENR

An Ethereum Node Record (ENR) is a signed, key-sorted map of name/value pairs identifying a node — its public key, IP, ports, and any per-protocol metadata. `EnrRecord` + `EnrRecordEncoder` (in `Nethereum.Model.Enr`) handle the data model and RLP shape; `EnrRecordSigner` (in `Nethereum.Signer.Enr`) produces and verifies the v4 secp256k1 signature.

## Quick Start

Run an inbound-only discv5 listener that auto-responds to pings, find-node and talk requests:

```csharp
using System.Net;
using System.Text;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;

var key = EthECKey.GenerateKey();
using var listener = new Discv5Listener(key);
listener.Start(IPAddress.Loopback, port: 0);

// Self-ENR is required so we can answer FindNode(distance=0) with our own record.
var enr = new EnrRecord { Sequence = 1 };
enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
enr.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
enr.Pairs["udp"] = new[] { (byte)((listener.Port >> 8) & 0xff), (byte)(listener.Port & 0xff) };
EnrRecordSigner.Sign(enr, key);
listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(enr);
listener.LocalEnrSequence = enr.Sequence;

var enrUrl = EnrRecordEncoder.ToUrl(enr);   // the enr:... URL to share with other nodes
```

## Usage Examples

### Accept inbound RLPx connections

```csharp
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;

var serverKey = EthECKey.GenerateKey();
var config = new DevP2PConfig { ClientId = "MyClient/1.0" };
var rlpx = new RlpxListener(serverKey, config);

rlpx.PeerAccepted += async (_, conn) =>
{
    // conn is fully handshaked; sub-protocol caps are negotiated.
    // Dispatch to your eth/68 or snap/1 session handler here.
};
rlpx.Start(port: 0, bindAddress: System.Net.IPAddress.Any);
```

### Serve eth/68 blocks from an `IEth68RequestHandler`

```csharp
using Nethereum.DevP2P.Sync;   // server session
using Nethereum.Model.P2P;

IEth68RequestHandler myHandler = new MyChainBackedHandler();  // serves headers/bodies/receipts from your store
var ethSession = new Eth68ServerSession(connection, myHandler, localStatus: myStatus);
// Drive ethSession from your RLPx receive loop:
//   var (msgId, payload) = await connection.ReceiveMessageAsync();
//   await ethSession.HandleEthMessageAsync(msgId - ethOffset, payload, ct);
```

### Serve snap/1 from a Patricia state root

```csharp
using Nethereum.DevP2P.Sync;
using Nethereum.Model.P2P.Snap;

var snapHandler = new PatriciaSnapRequestHandler(trieStorage, bytecodeStore);
var request = new GetAccountRangeMessage
{
    RootHash = stateRoot,
    StartingHash = startHash,
    LimitHash = limitHash,
    ResponseBytes = byteLimit
};
var range = await snapHandler.GetAccountRangeAsync(request, ct);
// `range.Accounts` holds the slim accounts; `range.Proof` is the edge proof the peer verifies.
```

## Conformance against go-ethereum

The package is exercised against go-ethereum's `cmd/devp2p` conformance tool — the canonical test harness the rest of the Ethereum client ecosystem uses. As of the most recent run:

| Suite | Pass |
|-------|------|
| `devp2p discv4 test` | 15 / 15 |
| `devp2p rlpx eth-test` | 19 / 19 |
| `devp2p rlpx snap-test` | 5 / 5 |
| `devp2p discv5 test` | 7 / 7 |

For details on how to rebuild the `devp2p` binary, run the suites, and the two non-obvious wire-spec discoveries made along the way, see `tests/Nethereum.DevP2P.IntegrationTests/README.md` in this repository.

## Related Packages

- **Nethereum.DevP2P.Sync** — higher-level sync orchestration built on top of this package: `Eth68ServerSession`, snap-sync client, multi-peer follower service.
- **Nethereum.RLP** — RLP encoding primitives used by every wire message.
- **Nethereum.Signer** — ECIES, ENR signatures, id-signature verification.
- **Nethereum.Model** — wire message models, ENR data class, signed transaction types.
