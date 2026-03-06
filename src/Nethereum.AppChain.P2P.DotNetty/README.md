# Nethereum.AppChain.P2P.DotNetty

> **PREVIEW** — This package is in preview. APIs may change between releases.

High-performance DotNetty-based P2P transport for [Nethereum AppChain](../Nethereum.AppChain/README.md) networking with TLS support, peer discovery, and binary message framing.

## Overview

This package implements the `IP2PTransport` interface using the DotNetty async I/O framework. It provides the network transport layer for AppChain nodes, handling TCP connections, binary message framing, TLS encryption, peer discovery via gossip, and connection lifecycle management.

The transport uses DotNetty's event loop architecture for high throughput with minimal GC pressure. It supports configurable connection limits, per-IP restrictions, handshake timeouts, and optional ECDSA-based peer authentication.

### Key Features

- **DotNetty Transport**: Zero-copy buffers and async I/O for high message throughput
- **TLS Support**: Optional TLS encryption with self-signed certs (dev) or loaded certificates (production)
- **Peer Discovery**: Gossip-based peer exchange for automatic network expansion
- **Binary Framing**: Length-prefixed message encoding with configurable max size (10 MB)
- **Connection Management**: Per-IP limits, total connection caps, idle detection, and keep-alive pings
- **Handshake Protocol**: Hello exchange with protocol version and chain ID verification

## Installation

```bash
dotnet add package Nethereum.AppChain.P2P.DotNetty
```

### Dependencies

- **DotNetty.Transport** - Core networking primitives and event loop groups
- **DotNetty.Codecs** - Length-field frame decoder/encoder for reliable message boundaries
- **DotNetty.Handlers** - TLS handler and idle state detection
- **Nethereum.CoreChain** - `IP2PTransport` interface, `P2PMessage`, `P2PMessageType`
- **Microsoft.Extensions.Logging.Abstractions** - Structured logging

## Key Concepts

### Handshake Protocol

1. **Initiator** connects and sends `Hello` message (protocol version, chain ID, node ID)
2. **Responder** validates chain ID and version, replies with own `Hello`
3. **Optional auth**: If configured, exchange `AuthChallenge`/`AuthResponse` with ECDSA signatures
4. **Discovery**: After handshake, exchange `GetPeers`/`Peers` messages
5. **Connection marked as active**: `PeerConnected` event fired

### Connection Limits

- **Max connections**: 50 total (configurable)
- **Target connections**: 25 (discovery loop stops when reached)
- **Per-IP limit**: 5 connections per IP address
- **Handshake timeout**: 30 seconds
- **Idle timeout**: 60 seconds triggers ping; 120 seconds disconnects

### Message Types

Messages exchanged over the transport (defined in `CoreChain.P2P`):

- `Hello`, `AuthChallenge`, `AuthResponse` - Handshake and authentication
- `Ping`, `Pong` - Keep-alive
- `GetPeers`, `Peers` - Peer discovery
- `NewBlock` - Block propagation
- `Disconnect` - Graceful disconnect with reason codes

## Quick Start

```csharp
using Nethereum.AppChain.P2P.DotNetty;

var config = DotNettyConfig.ForDevelopment(port: 30303, chainId: 420420);

var transport = new DotNettyTransport(config, logger);
await transport.StartAsync();

// Connect to a bootstrap peer
await transport.ConnectAsync("node-1", "127.0.0.1:30304");

// Broadcast a message
await transport.BroadcastAsync(new P2PMessage(P2PMessageType.NewBlock, blockData));
```

## Usage Examples

### Example 1: Configuration Presets

```csharp
using Nethereum.AppChain.P2P.DotNetty;

// Development: no TLS, relaxed limits
var dev = DotNettyConfig.ForDevelopment(port: 30303, chainId: 31337);

// Private network: specific allowed peers
var priv = DotNettyConfig.ForPrivateNetwork(
    chainId: 420420,
    nodePrivateKey: "0x...",
    allowedPeers: new[] { "0xaddr1", "0xaddr2" });

// Production: TLS enabled
var prod = DotNettyConfig.ForProduction(
    chainId: 1,
    nodePrivateKey: "0x...",
    tlsCertPath: "/path/to/cert.pfx",
    tlsCertPassword: "password");
```

### Example 2: Event-Driven Peer Monitoring

```csharp
var transport = new DotNettyTransport(config, logger);

transport.PeerConnected += (sender, args) =>
{
    Console.WriteLine($"Peer connected: {args.PeerId}");
};

transport.PeerDisconnected += (sender, args) =>
{
    Console.WriteLine($"Peer disconnected: {args.PeerId}");
};

transport.MessageReceived += (sender, args) =>
{
    Console.WriteLine($"Message from {args.PeerId}: {args.Message.Type}");
};

await transport.StartAsync();
```

## API Reference

### DotNettyTransport

High-performance P2P transport implementing `IP2PTransport`.

```csharp
public class DotNettyTransport : IP2PTransport, IAsyncDisposable
{
    public string NodeId { get; }
    public bool IsRunning { get; }
    public int ConnectedPeers { get; }

    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();
    public Task ConnectAsync(string peerId, string endpoint);
    public Task DisconnectAsync(string peerId);
    public Task BroadcastAsync(P2PMessage message, CancellationToken ct = default);
    public Task SendAsync(string peerId, P2PMessage message, CancellationToken ct = default);
    public bool IsConnected(string peerId);
    public PeerInfo[] GetKnownPeers(int maxCount = 25);
}
```

### DotNettyConfig

Transport configuration with factory presets.

Key properties:
- `ListenPort` (default: 30303) - TCP listen port
- `MaxConnections` (default: 50) - Total connection limit
- `MaxConnectionsPerIp` (default: 5) - Per-IP connection limit
- `UseTls` - Enable TLS encryption
- `MaxMessageSize` (default: 10 MB) - Maximum message payload
- `ConnectionTimeoutMs` (default: 10000) - TCP connection timeout
- `HandshakeTimeoutSeconds` (default: 30) - Handshake completion timeout

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.P2P.Server](../Nethereum.AppChain.P2P.Server/README.md)** - P2P server uses DotNetty transport
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - Main server uses DotNetty for Clique consensus

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - `IP2PTransport` interface and message types

## Additional Resources

- [DotNetty](https://github.com/Azure/DotNetty) - Async event-driven network framework
- [Nethereum Documentation](https://docs.nethereum.com)
