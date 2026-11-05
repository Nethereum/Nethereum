# Nethereum.Consensus.Clique

Pluggable Clique Proof-of-Authority (EIP-225) consensus engine for Nethereum CoreChain and AppChain.

## Overview

Nethereum.Consensus.Clique implements the Clique Proof-of-Authority consensus protocol as defined in [EIP-225](https://eips.ethereum.org/EIPS/eip-225). It provides a complete consensus engine that integrates with Nethereum's CoreChain block production pipeline through the `IConsensusEngine` and `IBlockProductionStrategy` interfaces.

Clique replaces Proof-of-Work mining with a set of authorized signers who take turns producing blocks. This makes it ideal for private networks, application chains, and development environments where deterministic block production, low resource usage, and fast finality are required.

The engine handles signer authorization, turn-based block scheduling, ECDSA block signing, snapshot-based state management, and dynamic validator set changes through an on-chain voting mechanism.

### Key Features

- **EIP-225 Compliant**: Full implementation of the Clique PoA specification
- **Turn-Based Scheduling**: In-turn and out-of-turn block production with configurable delays
- **Dynamic Validator Voting**: Add or remove signers via majority consensus without chain restarts
- **Epoch Snapshots**: Periodic signer list commitments in block headers for fast sync, checked against the snapshot when a checkpoint block is validated
- **Per-Parent Snapshots**: Every block is validated against the snapshot of its own parent, keyed by block hash, so a fork is judged on its own signer set
- **Block Period Enforced**: A block is refused when its timestamp is earlier than the parent's plus `BlockPeriodSeconds`
- **Restart Recovery**: Snapshots are saved every `CliqueEngine.SnapshotInterval` blocks through an `ICliqueSnapshotStore`, and the head snapshot is rebuilt after a restart
- **Pluggable Architecture**: Implements `IConsensusEngine` and `IBlockProductionStrategy` for seamless CoreChain integration
- **Thread-Safe State**: Lock-based snapshot management with in-memory caching of the 100 snapshots with the highest block numbers

## Installation

```bash
dotnet add package Nethereum.Consensus.Clique
```

### Dependencies

- **Nethereum.CoreChain** - Provides `IConsensusEngine`, `IBlockProductionStrategy`, `BlockProductionOptions`, and `ChainConfig`
- **Nethereum.Signer** - ECDSA key management (`EthECKey`) and signature recovery for block signing
- **Nethereum.Hex** - Hex encoding utilities for address and hash conversions
- **Microsoft.Extensions.Logging.Abstractions** - Optional structured logging

`Nethereum.Model` types such as `BlockHeader` and `BlockHeaderEncoder` are used for Clique-specific header encoding, but the reference is transitive (pulled in via Nethereum.CoreChain and Nethereum.Signer), not a direct project reference.

## Key Concepts

### In-Turn vs Out-of-Turn

Clique assigns each authorized signer a slot based on `blockNumber % totalSigners`. The signer whose turn it is produces with difficulty 2 (in-turn) and waits only until the parent's timestamp plus `BlockPeriodSeconds` has passed. All other signers produce with difficulty 1 (out-of-turn) and wait that same time plus a randomized delay (`WiggleTimeMs` + random jitter) to give the in-turn signer priority. A signer that sealed one of the last `signers / 2 + 1` blocks cannot produce (`CanProduceBlock` is `false`) until enough other signers have sealed.

```csharp
// Difficulty constants
const int DIFF_IN_TURN = 2;
const int DIFF_OUT_OF_TURN = 1;
```

### Extra Data Structure

Block headers encode consensus data in the `ExtraData` field:

- **Bytes 0-31**: Vanity data (32 bytes)
- **Bytes 32 to N**: Signer addresses (only at epoch boundaries, 20 bytes per signer)
- **Final 65 bytes**: ECDSA signature seal (recoverable)

### Snapshot State

`CliqueSnapshot` captures the consensus state at a specific block: the authorized signer set, active votes, vote tallies and the block's `Timestamp`. Snapshots are cached by block hash (the 100 with the highest block numbers) and cloned for thread safety. At epoch boundaries, all pending votes are cleared and the full signer list is committed to `ExtraData`; a checkpoint block whose listed signers differ from the snapshot is refused.

### Voting Mechanism

Signers vote to add or remove validators using the block header's `Coinbase` (vote target) and `Nonce` fields:

- `0xFFFFFFFFFFFFFFFF` - Vote to authorize (add)
- `0x0000000000000000` - Vote to revoke (remove)

When votes for a target reach majority (`totalSigners / 2 + 1`), the signer set updates automatically.

## Quick Start

```csharp
using Nethereum.Consensus.Clique;

var config = new CliqueConfig
{
    BlockPeriodSeconds = 1,
    EpochLength = 30000,
    InitialSigners = new List<string> { signerAddress },
    LocalSignerAddress = signerAddress,
    LocalSignerPrivateKey = privateKey,
    AllowEmptyBlocks = false,
    EnableVoting = true
};

var engine = new CliqueEngine(config);
engine.ApplyGenesisSigners(config.InitialSigners);
```

## Usage Examples

### Example 1: Configure Clique for AppChain

```csharp
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;

var cliqueConfig = new CliqueConfig
{
    BlockPeriodSeconds = 1,
    EpochLength = 30000,
    WiggleTimeMs = 200,
    InitialSigners = new List<string>(signerAddresses),
    LocalSignerAddress = account.Address,
    LocalSignerPrivateKey = privateKey,
    AllowEmptyBlocks = false,
    EnableVoting = true
};

var cliqueEngine = new CliqueEngine(cliqueConfig, logger);
cliqueEngine.ApplyGenesisSigners(signerAddresses);

var strategy = new CliqueBlockProductionStrategy(
    chainConfig, cliqueEngine, logger);
```

### Example 2: Use Configuration Presets

```csharp
using Nethereum.Consensus.Clique;

// Standard 15-second block time
var standard = CliqueConfig.Default;

// Fast 1-second blocks for testing
var fast = CliqueConfig.Fast;

// Instant blocks, no delay, empty blocks allowed
var dev = CliqueConfig.DevMode;
```

### Example 3: Validate and Apply Blocks

```csharp
// Validate an incoming block header
var result = engine.ValidateBlockInternal(header, parentHeader);
if (result.IsValid)
{
    // Signer recovered from signature
    string signer = result.Signer;

    // Apply block to update consensus state
    engine.ApplyBlock(header, signer, blockHash);
}
else
{
    Console.WriteLine($"Block rejected: {result.Error}");
}
```

### Example 3b: What Validation Refuses

`ValidateBlockInternal(header, parent)` returns `CliqueValidationResult.Fail` when:

- the supplied `parent` is not the parent of `header` (wrong parent hash or block number): `Unknown ancestor`;
- `header.Timestamp` is earlier than `parent.Timestamp + BlockPeriodSeconds`: `Invalid timestamp`;
- there is no snapshot for the parent: `Unknown ancestor`;
- the signer is not in the parent's snapshot, or signed within the recent-signers limit, or the difficulty does not match the signer's turn in that snapshot;
- a checkpoint block lists signers other than the snapshot's.

```csharp
var tooEarly = engine.ValidateBlockInternal(header, parent);
Console.WriteLine(tooEarly.Error);
```

### Example 4: Block Production Strategy Integration

```csharp
// Check if this node can produce the next block
if (strategy.CanProduceBlock(nextBlockNumber))
{
    // Get signing delay (time until parent timestamp + period for in-turn, plus a random wiggle for out-of-turn)
    var delay = await strategy.GetSigningDelayAsync(nextBlockNumber);
    await Task.Delay(delay);

    // Prepare block options with timestamp (never earlier than parent + period), difficulty and extra data
    var options = strategy.PrepareBlockOptions(nextBlockNumber, parentHeader);

    // After block execution, finalize (sign + apply state)
    await strategy.FinalizeBlockAsync(header, blockHash, result);
}
```

### Example 5: Persist Snapshots and Recover After a Restart

```csharp
var store = new InMemoryCliqueSnapshotStore();
var engine = new CliqueEngine(config, logger, store);

var gate = new CliqueConsensusBlockGate(engine, blockStore, logger);
await gate.RestoreHeadSnapshotAsync(ct);

bool known = engine.HasSnapshot(blockHash);
```

`ApplyBlock` saves a snapshot to the store whenever the block number is a multiple of `CliqueEngine.SnapshotInterval` (1024). `RestoreHeadSnapshotAsync` reads the head of the block store and rebuilds its snapshot, starting from the nearest persisted or checkpoint snapshot instead of walking from genesis; at genesis it adopts the genesis signers. Before it validates a block the gate also rebuilds any missing parent snapshot from the block store.

## API Reference

### CliqueEngine

Central consensus engine implementing `IConsensusEngine`.

```csharp
public class CliqueEngine : IConsensusEngine, IDisposable
{
    // Initialization
    public const int SnapshotInterval = 1024;
    public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger = null);
    public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger, ICliqueSnapshotStore? snapshotStore);
    public void ApplyGenesisSigners(IEnumerable<string> signers);

    // Consensus queries
    public bool IsInTurn(long blockNumber, string signerAddress);
    public BigInteger GetDifficulty(long blockNumber, string signerAddress);
    public bool CanProduceBlock(long blockNumber);
    public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default);
    public long NextBlockTimestamp(BlockHeader? parent);
    public bool HasSnapshot(byte[] blockHash);

    // Block signing and validation
    public string AuthorOf(BlockHeader header);
    public byte[] SignBlock(BlockHeader header);
    public string? RecoverSigner(BlockHeader header);
    public bool ValidateBlock(BlockHeader header, BlockHeader? parent);
    public CliqueValidationResult ValidateBlockInternal(BlockHeader header, BlockHeader? parent);

    // State management
    public void ApplyBlock(BlockHeader header, string signer, byte[]? blockHash = null);
    public byte[] PrepareExtraData(long blockNumber, object? vote = null);
}
```

`CanProduceBlock(blockNumber)` is `false` when no signer key is configured, when the local signer is outside the signer set, or when it sealed one of the last blocks within the recent-signers limit. `GetSigningDelayAsync` returns the time until the head snapshot's timestamp plus `BlockPeriodSeconds` for an in-turn signer, and that plus `WiggleTimeMs` and a random 0 to `WiggleTimeMs` for an out-of-turn signer. `NextBlockTimestamp(parent)` is the larger of the parent's timestamp plus `BlockPeriodSeconds` and the current Unix time (the current time when `parent` is `null`). `AuthorOf(header)` returns the local signer address when `header.ExtraData` is `null` or shorter than 97 bytes (`EXTRA_VANITY` + `EXTRA_SEAL`) or when its final 65 seal bytes are all zero, otherwise the signer recovered from the seal, or `header.Coinbase` when recovery from the seal fails. `HasSnapshot(blockHash)` checks the in-memory cache and then the `ICliqueSnapshotStore`.

### CliqueConsensusBlockGate

Validates each block against its parent's snapshot before it is executed and keeps the snapshots in step with the block store.

```csharp
public sealed class CliqueConsensusBlockGate : IConsensusBlockGate
{
    public CliqueConsensusBlockGate(CliqueEngine engine, IBlockStore blocks, ILogger? logger = null);
    public Task RestoreHeadSnapshotAsync(CancellationToken ct);
}
```

### ICliqueSnapshotStore and CliqueSnapshotCodec

```csharp
public interface ICliqueSnapshotStore
{
    void Save(CliqueSnapshot snapshot);
    CliqueSnapshot? Load(byte[] blockHash);
}

public sealed class InMemoryCliqueSnapshotStore : ICliqueSnapshotStore { }

public static class CliqueSnapshotCodec
{
    public static byte[] Encode(CliqueSnapshot snapshot);
    public static CliqueSnapshot? Decode(byte[] encoded);
}
```

`CliqueSnapshotCodec.Decode` returns `null` for a truncated or corrupt buffer instead of throwing. `Load` must return the snapshot saved for exactly that block hash; the engine ignores a snapshot whose `BlockHash` differs.

### CliqueBlockProductionStrategy

Bridge between CoreChain block production and Clique consensus.

```csharp
public class CliqueBlockProductionStrategy : IBlockProductionStrategy
{
    public CliqueBlockProductionStrategy(
        ChainConfig chainConfig,
        CliqueEngine cliqueEngine,
        ILogger<CliqueBlockProductionStrategy>? logger = null,
        ICliqueProposalStore? proposalStore = null);

    public bool CanProduceBlock(long blockNumber);
    public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default);
    public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader);
    public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result);

    public event EventHandler<BlockFinalizedEventArgs>? BlockFinalized;
}
```

### CliqueConfig

Configuration parameters with factory presets.

Key properties:
- `BlockPeriodSeconds` (default: 15) - Target time between blocks
- `EpochLength` (default: 30000) - Blocks between signer list commits
- `WiggleTimeMs` (default: 500) - Out-of-turn randomization delay
- `InitialSigners` - Genesis validator addresses
- `LocalSignerAddress` / `LocalSignerPrivateKey` - This node's identity
- `AllowEmptyBlocks` - Whether to produce blocks with no transactions
- `EnableVoting` - Whether dynamic signer changes are allowed

### CliqueSnapshot

Point-in-time consensus state (mutable; `Clone()` produces an independent copy for thread safety).

Key properties:
- `BlockNumber`, `BlockHash`, `Timestamp` - The block the snapshot describes
- `Signers` - Ordered list of authorized signer addresses
- `Votes` - Active voting intents
- `VoteTally` - Aggregated vote counts
- `RequiredVotes` - Majority threshold (`TotalSigners / 2 + 1`)

### CliqueValidationResult

Block validation outcome.

- `CliqueValidationResult.Success(signer)` - Valid block with recovered signer
- `CliqueValidationResult.Fail(error)` - Invalid block with error message

## Related Packages

### Used By (Consumers)
- **[Nethereum.AppChain.Server](../Nethereum.AppChain.Server/README.md)** - Uses Clique for multi-validator AppChain consensus

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Consensus interfaces and block production pipeline
- **[Nethereum.Signer](../Nethereum.Signer/README.md)** - ECDSA cryptographic operations
- **[Nethereum.Model](../Nethereum.Model/README.md)** - Block header data structures and encoding (transitive, via CoreChain/Signer)

## Additional Resources

- [EIP-225: Clique Proof-of-Authority](https://eips.ethereum.org/EIPS/eip-225)
- [Nethereum Documentation](https://docs.nethereum.com)
