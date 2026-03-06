# Nethereum.AppChain.P2P

> **PREVIEW** — This package is in preview. APIs may change between releases.

Transport-agnostic peer-to-peer security components for [Nethereum AppChain](../Nethereum.AppChain/README.md) networking — securing the layer that makes state synchronisable by anyone.

## Overview

For AppChain state to be independently verifiable, the network layer must be secure. This package provides application-layer security infrastructure: reputation scoring to track peer quality, rate limiting to prevent message flooding, and ECDSA-based authentication to verify peer identity.

It also provides block handling components that validate incoming blocks against consensus rules and broadcast produced blocks to the network. All components are transport-agnostic and designed to work with any P2P transport implementation (e.g., DotNetty).

### Key Features

- **Peer Reputation**: Score-based peer quality tracking with automatic banning and exponential ban escalation
- **Rate Limiting**: Dual-level (per-peer and global) sliding window rate limiting by message category
- **ECDSA Authentication**: Challenge-response peer authentication with nonce and timestamp validation
- **Block Handling**: Consensus-validated block import with sequential processing guarantees
- **Block Broadcasting**: Binary-encoded block propagation to connected peers
- **Thread-Safe**: `ConcurrentDictionary`-based state with background cleanup

## Installation

```bash
dotnet add package Nethereum.AppChain.P2P
```

### Dependencies

- **Nethereum.CoreChain** - `IBlockStore`, `IConsensusEngine`, `BlockHeader`, `BlockHeaderEncoder`
- **Nethereum.Signer** - `EthECKey` for ECDSA operations and Keccak hashing
- **Nethereum.Model** - Block header and transaction data structures
- **Microsoft.Extensions.Logging.Abstractions** - Structured logging

## Key Concepts

### Reputation System

`PeerReputationManager` maintains a score (-1000 to 1000) for each peer. Positive events (valid blocks, successful syncs) increase the score; negative events (invalid blocks, spam, protocol violations) decrease it. Peers are banned when their score drops below -100, with ban duration doubling on each successive ban.

```csharp
var reputationManager = new PeerReputationManager(new ReputationConfig());
reputationManager.Start();

reputationManager.RecordPositive(peerId, ReputationEvent.ValidBlock);     // +10
reputationManager.RecordNegative(peerId, ReputationEvent.InvalidBlock);   // -50
```

### Rate Limiting

`PeerRateLimiter` enforces per-peer and global rate limits using sliding windows across four categories: Messages, Blocks, Transactions, and Requests. Peers exceeding limits are automatically banned after 10 violations.

### Block Import Pipeline

`P2PBlockHandler` processes incoming blocks sequentially (SemaphoreSlim) to ensure consistency:
1. Decode block header from binary payload
2. Verify parent hash matches local chain head
3. Validate block number is sequential
4. Run consensus validation via `IConsensusEngine`
5. Store block and emit `BlockImported` event

## Quick Start

```csharp
using Nethereum.AppChain.P2P;

var reputationManager = new PeerReputationManager(new ReputationConfig());
reputationManager.Start();

var rateLimiter = new PeerRateLimiter(new RateLimitConfig());
rateLimiter.Start();

var blockHandler = new P2PBlockHandler(blockStore, consensusEngine, logger);
blockHandler.BlockImported += (sender, args) =>
{
    Console.WriteLine($"Block {args.Header.BlockNumber} from peer {args.FromPeerId}");
};
```

## Usage Examples

### Example 1: Peer Authentication

```csharp
using Nethereum.AppChain.P2P;

var authenticator = new PeerAuthenticator(new PeerAuthConfig
{
    RequireAuthentication = true,
    MaxTimestampDriftSeconds = 30
});

// Create and sign challenge
byte[] challenge = authenticator.CreateChallenge();
byte[] signature = authenticator.SignChallenge(challenge, signerKey);

// Verify peer
var result = authenticator.Authenticate(peerId, challenge, signature);
if (result.IsSuccess)
{
    Console.WriteLine($"Peer authenticated as {result.Address}");
}
```

### Example 2: Rate Limiting with Categories

```csharp
using Nethereum.AppChain.P2P;

var limiter = new PeerRateLimiter(new RateLimitConfig
{
    // Per-peer limits per 60-second window
    PerPeerLimits = new Dictionary<RateLimitCategory, int>
    {
        [RateLimitCategory.Messages] = 1000,
        [RateLimitCategory.Blocks] = 100,
        [RateLimitCategory.Transactions] = 500
    },
    MaxViolationsBeforeBan = 10
});

limiter.Start();

var result = limiter.CheckLimit(peerId, RateLimitCategory.Blocks);
if (!result.IsAllowed)
{
    Console.WriteLine($"Rate limited. Retry after: {result.RetryAfter}");
}
```

## API Reference

### PeerReputationManager

Score-based peer quality tracking with automatic banning.

Key methods:
- `RecordPositive(peerId, ReputationEvent)` - Increase peer score
- `RecordNegative(peerId, ReputationEvent)` - Decrease peer score
- `GetScore(peerId) : int` - Current reputation score
- `IsBanned(peerId) : bool` - Check ban status
- `GetTopPeers(count)` / `GetBottomPeers(count)` - Ranked peer lists

### PeerRateLimiter

Sliding window rate limiting by category.

Key methods:
- `CheckLimit(peerId, category, cost) : RateLimitResult` - Check and consume quota
- `ReportViolation(peerId, ViolationType)` - Record protocol violation
- `IsBanned(peerId) : bool` - Check ban status

### P2PBlockHandler

Consensus-validated block import.

Key methods:
- `HandleNewBlockMessageAsync(payload, peerId) : BlockImportResult` - Process binary block message
- `HandleNewBlockAsync(header, blockHash, peerId) : BlockImportResult` - Process decoded block

Events: `BlockImported`, `BlockRejected`

### PeerAuthenticator

ECDSA challenge-response peer authentication.

Key methods:
- `CreateChallenge() : byte[]` - Generate random challenge
- `SignChallenge(challenge, key) : byte[]` - Sign with node key
- `Authenticate(peerId, challenge, signature) : AuthResult` - Verify peer

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.P2P.DotNetty](../Nethereum.AppChain.P2P.DotNetty/README.md)** - DotNetty transport wraps these security components

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Consensus and storage interfaces
- **[Nethereum.Signer](../Nethereum.Signer/README.md)** - Cryptographic operations

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
