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
- **Epoch Snapshots**: Periodic signer list commitments in block headers for fast sync
- **Pluggable Architecture**: Implements `IConsensusEngine` and `IBlockProductionStrategy` for seamless CoreChain integration
- **Thread-Safe State**: Lock-based snapshot management with in-memory caching

## Installation

```bash
dotnet add package Nethereum.Consensus.Clique
```

### Dependencies

- **Nethereum.CoreChain** - Provides `IConsensusEngine`, `IBlockProductionStrategy`, `BlockProductionOptions`, and `ChainConfig`
- **Nethereum.Signer** - ECDSA key management (`EthECKey`) and signature recovery for block signing
- **Nethereum.Model** - `BlockHeader` and `BlockHeaderEncoder` for Clique-specific header encoding
- **Nethereum.Hex** - Hex encoding utilities for address and hash conversions
- **Microsoft.Extensions.Logging.Abstractions** - Optional structured logging

## Key Concepts

### In-Turn vs Out-of-Turn

Clique assigns each authorized signer a slot based on `blockNumber % totalSigners`. The signer whose turn it is produces with difficulty 2 (in-turn) and zero delay. All other signers produce with difficulty 1 (out-of-turn) and wait a randomized delay (`WiggleTimeMs` + random jitter) to give the in-turn signer priority.

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

`CliqueSnapshot` captures the consensus state at a specific block: the authorized signer set, active votes, and vote tallies. Snapshots are cached (100 most recent) and cloned for thread safety. At epoch boundaries, all pending votes are cleared and the full signer list is committed to `ExtraData`.

### Voting Mechanism

Signers vote to add or remove validators using the block header's `Coinbase` (vote target) and `Nonce` fields:

- `0x0000000000000000` - Vote to authorize (add)
- `0xFFFFFFFFFFFFFFFF` - Vote to revoke (remove)

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

### Example 4: Block Production Strategy Integration

```csharp
// Check if this node can produce the next block
if (strategy.CanProduceBlock(nextBlockNumber))
{
    // Get signing delay (zero for in-turn, random for out-of-turn)
    var delay = await strategy.GetSigningDelayAsync(nextBlockNumber);
    await Task.Delay(delay);

    // Prepare block options with difficulty and extra data
    var options = strategy.PrepareBlockOptions(nextBlockNumber, parentHeader);

    // After block execution, finalize (sign + apply state)
    await strategy.FinalizeBlockAsync(header, blockHash, result);
}
```

## API Reference

### CliqueEngine

Central consensus engine implementing `IConsensusEngine`.

```csharp
public class CliqueEngine : IConsensusEngine, IDisposable
{
    // Initialization
    public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger = null);
    public void ApplyGenesisSigners(IEnumerable<string> signers);

    // Consensus queries
    public bool IsInTurn(long blockNumber, string signerAddress);
    public BigInteger GetDifficulty(long blockNumber, string signerAddress);
    public bool CanProduceBlock(long blockNumber);
    public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken ct);

    // Block signing and validation
    public byte[] SignBlock(BlockHeader header);
    public string? RecoverSigner(BlockHeader header);
    public bool ValidateBlock(BlockHeader header, BlockHeader? parent);
    public CliqueValidationResult ValidateBlockInternal(BlockHeader header, BlockHeader? parent);

    // State management
    public void ApplyBlock(BlockHeader header, string signer, byte[]? blockHash = null);
    public byte[] PrepareExtraData(long blockNumber, object? vote = null);
}
```

### CliqueBlockProductionStrategy

Bridge between CoreChain block production and Clique consensus.

```csharp
public class CliqueBlockProductionStrategy : IBlockProductionStrategy
{
    public CliqueBlockProductionStrategy(ChainConfig chainConfig, CliqueEngine engine, ILogger? logger = null);

    public bool CanProduceBlock(long blockNumber);
    public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken ct);
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

Immutable point-in-time consensus state.

Key properties:
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
- **[Nethereum.AppChain.P2P.Server](../Nethereum.AppChain.P2P.Server/README.md)** - Uses Clique with DotNetty P2P networking

### Dependencies
- **[Nethereum.CoreChain](../Nethereum.CoreChain/README.md)** - Consensus interfaces and block production pipeline
- **[Nethereum.Signer](../Nethereum.Signer/README.md)** - ECDSA cryptographic operations
- **[Nethereum.Model](../Nethereum.Model/README.md)** - Block header data structures and encoding

## Additional Resources

- [EIP-225: Clique Proof-of-Authority](https://eips.ethereum.org/EIPS/eip-225)
- [Nethereum Documentation](https://docs.nethereum.com)
