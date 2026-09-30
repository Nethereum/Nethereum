# Nethereum.Consensus.Ssz

SSZ container implementations for Ethereum consensus layer light client types, including beacon block headers, sync committees, and light client data structures.

## Overview

Nethereum.Consensus.Ssz provides strongly-typed SSZ containers for Ethereum's consensus layer (beacon chain), specifically designed for light client synchronization. This package builds on top of `Nethereum.Ssz` primitives to implement complete consensus data structures with encoding, decoding, and hash tree root computation.

**Key Features:**
- Beacon block header containers with SSZ serialization
- Light client sync data structures (bootstrap, headers, updates)
- Sync committee representations (512 validator public keys)
- Execution payload headers for post-merge Ethereum
- Hash tree root computation for merkle verification
- Type-safe encoding/decoding with compile-time validation

## Installation

```bash
dotnet add package Nethereum.Consensus.Ssz
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Consensus.Ssz
```

## Key Concepts

### Consensus Layer vs Execution Layer

Ethereum uses **two different serialization formats**:

| Aspect | Execution Layer | Consensus Layer |
|--------|----------------|-----------------|
| **Format** | RLP (Recursive Length Prefix) | SSZ (Simple Serialize) |
| **Hash Function** | Keccak-256 | SHA-256 |
| **Byte Order** | Big-endian | Little-endian |
| **Use Cases** | Transactions, blocks, accounts | Beacon blocks, attestations, validators |

This package implements **consensus layer** types using SSZ.

### Core Container Types

1. **BeaconBlockHeader** (112 bytes fixed): Core beacon chain block header
   - Slot number (8 bytes)
   - Proposer validator index (8 bytes)
   - Parent root (32 bytes)
   - State root (32 bytes)
   - Body root (32 bytes)

2. **SyncCommittee** (24,624 bytes): Committee of 512 validators
   - 512 BLS public keys (48 bytes each = 24,576 bytes)
   - Aggregate public key (48 bytes)

3. **LightClientHeader**: Beacon header with execution layer payload
   - Beacon block header
   - Execution payload header
   - Merkle branch for verification

4. **LightClientBootstrap**: Initial sync data for light clients
   - Current header
   - Current sync committee
   - Merkle branch

### SSZ Container Pattern

All containers follow a consistent interface:

```csharp
public class Container
{
    // Serialize to SSZ bytes
    public byte[] Encode() { }

    // Deserialize from SSZ bytes
    public static Container Decode(ReadOnlySpan<byte> data) { }

    // Compute SHA-256 merkle root
    public byte[] HashTreeRoot() { }
}
```

## Quick Start

```csharp
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

// Create and encode a beacon block header
var header = new BeaconBlockHeader
{
    Slot = 1234567,
    ProposerIndex = 42,
    ParentRoot = new byte[32],
    StateRoot = new byte[32],
    BodyRoot = new byte[32]
};

// Serialize to SSZ
byte[] encoded = header.Encode();

// Deserialize from SSZ
var decoded = BeaconBlockHeader.Decode(encoded);

// Compute hash tree root for verification
byte[] root = header.HashTreeRoot();
Console.WriteLine($"Block root: {root.ToHex(true)}");
```

## Usage Examples

### Example 1: BeaconBlockHeader Encoding and Decoding

```csharp
using Nethereum.Consensus.Ssz;

// Create a beacon block header
var header = new BeaconBlockHeader
{
    Slot = 5000000,
    ProposerIndex = 123456,
    ParentRoot = new byte[32], // 32-byte root
    StateRoot = new byte[32],  // 32-byte root
    BodyRoot = new byte[32]    // 32-byte root
};

// Encode to SSZ bytes (always 112 bytes)
byte[] sszBytes = header.Encode();
Console.WriteLine($"Encoded length: {sszBytes.Length}"); // 112

// Decode back to object
var decoded = BeaconBlockHeader.Decode(sszBytes);

// Verify round-trip
Assert.Equal(header.Slot, decoded.Slot);
Assert.Equal(header.ProposerIndex, decoded.ProposerIndex);
```

### Example 2: SyncCommittee Round-Trip

```csharp
using Nethereum.Consensus.Ssz;

// Create sync committee with 512 public keys
var syncCommittee = new SyncCommittee();

// Each public key is 48 bytes (BLS12-381)
syncCommittee.PubKeys = new List<byte[]>();
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
{
    var pubkey = new byte[SszBasicTypes.PubKeyLength];
    pubkey[0] = (byte)(i % 256);
    pubkey[1] = (byte)(i / 256);
    syncCommittee.PubKeys.Add(pubkey);
}

// Aggregate public key
syncCommittee.AggregatePubKey = new byte[SszBasicTypes.PubKeyLength];

// Encode (24,624 bytes total)
byte[] encoded = syncCommittee.Encode();
Console.WriteLine($"Size: {encoded.Length}"); // 24,624

// Decode
var decoded = SyncCommittee.Decode(encoded);
Assert.Equal(512, decoded.PubKeys.Count);
```

### Example 3: Hash Tree Root Computation

```csharp
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

var header = new BeaconBlockHeader
{
    Slot = 1000,
    ProposerIndex = 50,
    ParentRoot = new byte[32],
    StateRoot = new byte[32],
    BodyRoot = new byte[32]
};

// Compute SHA-256 merkle root
byte[] root = header.HashTreeRoot();

// This root can be used for:
// 1. Block identification
// 2. Merkle proof verification
// 3. Light client sync
Console.WriteLine($"Block root: {root.ToHex(true)}");

// The root is deterministic - same input = same root
byte[] root2 = header.HashTreeRoot();
Assert.True(root.SequenceEqual(root2));
```

### Example 4: LightClientBootstrap Creation

```csharp
using Nethereum.Consensus.Ssz;

// Bootstrap data for a Capella light client. The container Fork, the nested
// Header.Fork and the fork passed to Encode/Decode must all agree.
var fork = ConsensusFork.Capella;

var bootstrap = new LightClientBootstrap { Fork = fork };

// Current header. Capella+ headers carry an ExecutionPayloadHeader (same Fork)
// plus a 4-root execution branch (ExecutionBranchDepth = 4).
bootstrap.Header = new LightClientHeader
{
    Fork = fork,
    Beacon = new BeaconBlockHeader
    {
        Slot = 5000000,
        ProposerIndex = 1000,
        ParentRoot = new byte[32],
        StateRoot = new byte[32],
        BodyRoot = new byte[32]
    },
    Execution = new ExecutionPayloadHeader
    {
        Fork = fork,
        ParentHash = new byte[32],
        FeeRecipient = new byte[20],
        StateRoot = new byte[32],
        ReceiptsRoot = new byte[32],
        LogsBloom = new byte[256],
        PrevRandao = new byte[32],
        ExtraData = new byte[0],
        BaseFeePerGas = new byte[32],
        BlockHash = new byte[32],
        TransactionsRoot = new byte[32],
        WithdrawalsRoot = new byte[32] // Capella+
    },
    ExecutionBranch = new List<byte[]>
    {
        new byte[32], new byte[32], new byte[32], new byte[32] // depth 4
    }
};

// Current sync committee: exactly 512 pubkeys (48 bytes each) + aggregate
bootstrap.CurrentSyncCommittee = new SyncCommittee
{
    PubKeys = new List<byte[]>(),
    AggregatePubKey = new byte[48]
};
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
{
    bootstrap.CurrentSyncCommittee.PubKeys.Add(new byte[48]);
}

// current_sync_committee_branch: 5 roots for Altair-Deneb, 6 for Electra+
bootstrap.CurrentSyncCommitteeBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32], new byte[32]
};

// Encode for transmission (fork selects the container shape)
byte[] encoded = bootstrap.Encode(fork);

// Light client can decode and verify - the same fork must be supplied
var decoded = LightClientBootstrap.Decode(encoded, fork);
```

### Example 5: ExecutionPayloadHeader Handling

```csharp
using Nethereum.Consensus.Ssz;

// Post-merge blocks include execution layer data
var executionHeader = new ExecutionPayloadHeader
{
    Fork = ConsensusFork.Bellatrix, // fork the header belongs to
    ParentHash = new byte[32],
    FeeRecipient = new byte[20], // Ethereum address
    StateRoot = new byte[32],
    ReceiptsRoot = new byte[32],
    LogsBloom = new byte[256],
    PrevRandao = new byte[32],
    BlockNumber = 15537394, // Example merge block
    GasLimit = 30000000,
    GasUsed = 12000000,
    Timestamp = 1663224162,
    ExtraData = new byte[0],
    BaseFeePerGas = new byte[32], // 32-byte little-endian uint256
    BlockHash = new byte[32],
    TransactionsRoot = new byte[32]
    // Capella adds WithdrawalsRoot; Deneb adds BlobGasUsed and ExcessBlobGas
};

// Encode execution header (no-arg Encode uses the Fork set above)
byte[] encoded = executionHeader.Encode();

// Decode - the fork must be supplied so the right fields are read
var decoded = ExecutionPayloadHeader.Decode(encoded, ConsensusFork.Bellatrix);

Console.WriteLine($"Block number: {decoded.BlockNumber}");
Console.WriteLine($"Gas used: {decoded.GasUsed}/{decoded.GasLimit}");
```

### Example 6: LightClientHeader with Execution Payload

```csharp
using Nethereum.Consensus.Ssz;

var lightClientHeader = new LightClientHeader();

// Beacon layer header
lightClientHeader.Beacon = new BeaconBlockHeader
{
    Slot = 5000000,
    ProposerIndex = 42,
    ParentRoot = new byte[32],
    StateRoot = new byte[32],
    BodyRoot = new byte[32]
};

// The fork the header belongs to (Capella+ carries an execution payload)
lightClientHeader.Fork = ConsensusFork.Capella;

// Execution layer header (post-merge) - its Fork must match the outer fork
lightClientHeader.Execution = new ExecutionPayloadHeader
{
    Fork = ConsensusFork.Capella,
    BlockNumber = 15537394,
    BlockHash = new byte[32],
    ParentHash = new byte[32],
    // ... other fields
};

// Merkle branch proving execution payload inclusion (depth 4, Capella+)
lightClientHeader.ExecutionBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32]
};

// Encode complete header
byte[] encoded = lightClientHeader.Encode(ConsensusFork.Capella);

// Light client uses this to verify execution layer blocks - supply the same fork
var decoded = LightClientHeader.Decode(encoded, ConsensusFork.Capella);
```

### Example 7: Fixed vs Dynamic Section Handling

```csharp
using Nethereum.Consensus.Ssz;

// SSZ containers have two sections:
// 1. Fixed: Known size at compile time
// 2. Dynamic: Variable size (lists, variable bytes)

var header = new BeaconBlockHeader
{
    Slot = 100,              // Fixed: 8 bytes
    ProposerIndex = 200,     // Fixed: 8 bytes
    ParentRoot = new byte[32], // Fixed: 32 bytes
    StateRoot = new byte[32],  // Fixed: 32 bytes
    BodyRoot = new byte[32]    // Fixed: 32 bytes
};

// BeaconBlockHeader is entirely fixed: 112 bytes
byte[] encoded = header.Encode();
Assert.Equal(112, encoded.Length);

// Containers with variable-length fields use offsets. At Capella+ the
// LightClientHeader is variable-size (its ExecutionPayloadHeader.extra_data),
// so the bootstrap encodes it via an offset in the fixed section.
var fork = ConsensusFork.Capella;

var syncCommittee = new SyncCommittee
{
    PubKeys = new List<byte[]>(),
    AggregatePubKey = new byte[48]
};
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++) // exactly 512
{
    syncCommittee.PubKeys.Add(new byte[48]);
}

var bootstrap = new LightClientBootstrap
{
    Fork = fork,
    Header = new LightClientHeader
    {
        Fork = fork,
        Beacon = new BeaconBlockHeader
        {
            ParentRoot = new byte[32],
            StateRoot = new byte[32],
            BodyRoot = new byte[32]
        },
        Execution = new ExecutionPayloadHeader
        {
            Fork = fork,
            FeeRecipient = new byte[20],
            LogsBloom = new byte[256],
            ExtraData = new byte[0]
        },
        ExecutionBranch = new List<byte[]>
        {
            new byte[32], new byte[32], new byte[32], new byte[32] // depth 4
        }
    },
    CurrentSyncCommittee = syncCommittee,
    CurrentSyncCommitteeBranch = new List<byte[]>
    {
        new byte[32], new byte[32], new byte[32], new byte[32], new byte[32] // 5 roots
    }
};

// Dynamic (variable-size) header comes after the fixed section,
// which holds a 4-byte offset pointing to it.
byte[] dynamicEncoded = bootstrap.Encode(fork);
```

### Example 8: Container Verification Pattern

```csharp
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

// Light client verification workflow
public bool VerifyLightClientUpdate(
    byte[] trustedRoot,
    LightClientHeader receivedHeader)
{
    // 1. Compute hash tree root of received header
    byte[] computedRoot = receivedHeader.HashTreeRoot();

    // 2. Compare with trusted root
    if (!computedRoot.SequenceEqual(trustedRoot))
    {
        Console.WriteLine("Header root mismatch!");
        return false;
    }

    // 3. Verify merkle branch for execution payload
    if (receivedHeader.ExecutionBranch != null)
    {
        // Merkle proof verification would go here
        // using the execution branch
    }

    Console.WriteLine($"Header verified: slot {receivedHeader.Beacon.Slot}");
    return true;
}
```

### Example 9: Complete Light Client Sync Flow

```csharp
using Nethereum.Consensus.Ssz;
using Nethereum.Hex.HexConvertors.Extensions;

// Step 1: Bootstrap light client (Bellatrix header is a bare beacon header)
var bootstrap = new LightClientBootstrap { Fork = ConsensusFork.Bellatrix };
bootstrap.Header = new LightClientHeader
{
    Fork = ConsensusFork.Bellatrix,
    Beacon = new BeaconBlockHeader
    {
        Slot = 5000000,
        ProposerIndex = 100,
        ParentRoot = new byte[32],
        StateRoot = new byte[32],
        BodyRoot = new byte[32]
    }
};

bootstrap.CurrentSyncCommittee = new SyncCommittee
{
    PubKeys = new List<byte[]>(),
    AggregatePubKey = new byte[48]
};

for (int i = 0; i < 512; i++)
{
    bootstrap.CurrentSyncCommittee.PubKeys.Add(new byte[48]);
}

// current_sync_committee_branch: 5 roots for Altair-Deneb (Bellatrix included)
bootstrap.CurrentSyncCommitteeBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32], new byte[32]
};

// Step 2: Encode and transmit
byte[] bootstrapData = bootstrap.Encode(ConsensusFork.Bellatrix);
Console.WriteLine($"Bootstrap size: {bootstrapData.Length} bytes");

// Step 3: Light client receives and decodes with the same fork
var decoded = LightClientBootstrap.Decode(bootstrapData, ConsensusFork.Bellatrix);

// Step 4: Verify sync committee
byte[] syncCommitteeRoot = decoded.CurrentSyncCommittee.HashTreeRoot();
Console.WriteLine($"Sync committee root: {syncCommitteeRoot.ToHex(true)}");

// Step 5: Now light client can verify future updates using this committee
Console.WriteLine($"Light client synced to slot: {decoded.Header.Beacon.Slot}");
Console.WriteLine($"Sync committee size: {decoded.CurrentSyncCommittee.PubKeys.Count}");
```

## API Reference

### ConsensusFork

Consensus-layer hard forks affecting the `LightClient*` and `ExecutionPayloadHeader` container shapes, merkleization indices, and proof depths. Ordering is chronological, so comparisons such as `fork >= ConsensusFork.Capella` are spec-meaningful. Every fork-aware `Encode`/`Decode`/`HashTreeRoot` overload takes a `ConsensusFork` (the parameterless overloads use the container's `Fork` property).

```csharp
public enum ConsensusFork
{
    Phase0 = 0,
    Altair = 1,
    Bellatrix = 2,   // execution payload header first appears (HasExecutionPayloadContainer)
    Capella = 3,     // LightClientHeader gains execution payload; adds WithdrawalsRoot
    Deneb = 4,       // adds BlobGasUsed / ExcessBlobGas
    Electra = 5,     // wider sync-committee / finality branches
    Fulu = 6,
    Gloas = 7
}
```

The related helper `LightClientForkSpec` exposes the per-fork constants (branch lengths, gindices, and predicates such as `HasExecutionPayloadHeader`, `HasWithdrawalsRoot`, `HasBlobGasFields`) that these containers use internally.

### BeaconBlockHeader

Core beacon chain block header (112 bytes fixed size).

```csharp
public class BeaconBlockHeader
{
    public ulong Slot { get; set; }              // 8 bytes
    public ulong ProposerIndex { get; set; }     // 8 bytes
    public byte[] ParentRoot { get; set; }       // 32 bytes
    public byte[] StateRoot { get; set; }        // 32 bytes
    public byte[] BodyRoot { get; set; }         // 32 bytes

    public byte[] Encode();
    public static BeaconBlockHeader Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
}
```

### SyncCommittee

Sync committee of 512 validators (24,624 bytes).

```csharp
public class SyncCommittee
{
    public IList<byte[]> PubKeys { get; set; }     // 512 x 48 bytes
    public byte[] AggregatePubKey { get; set; }    // 48 bytes

    public byte[] Encode();
    public static SyncCommittee Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
}
```

### LightClientHeader

Beacon header with execution payload.

```csharp
public class LightClientHeader
{
    public ConsensusFork Fork { get; set; }
    public BeaconBlockHeader Beacon { get; set; }
    public ExecutionPayloadHeader Execution { get; set; }
    public IList<byte[]> ExecutionBranch { get; set; }

    public byte[] Encode();                       // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static LightClientHeader Decode(byte[] data, ConsensusFork fork);
    public byte[] HashTreeRoot();                 // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### LightClientBootstrap

Initial sync data for light clients.

```csharp
public class LightClientBootstrap
{
    public ConsensusFork Fork { get; set; }
    public LightClientHeader Header { get; set; }
    public SyncCommittee CurrentSyncCommittee { get; set; }
    public IList<byte[]> CurrentSyncCommitteeBranch { get; set; }

    public byte[] Encode();                       // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static LightClientBootstrap Decode(byte[] data, ConsensusFork fork);
    public byte[] HashTreeRoot();                 // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### LightClientUpdate

Full light client update with next sync committee and finality proof.

```csharp
public class LightClientUpdate
{
    public ConsensusFork Fork { get; set; }
    public LightClientHeader AttestedHeader { get; set; }
    public SyncCommittee NextSyncCommittee { get; set; }
    public IList<byte[]> NextSyncCommitteeBranch { get; set; }
    public LightClientHeader FinalizedHeader { get; set; }
    public IList<byte[]> FinalityBranch { get; set; }
    public SyncAggregate SyncAggregate { get; set; }
    public ulong SignatureSlot { get; set; }

    public byte[] Encode();                       // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static LightClientUpdate Decode(byte[] data, ConsensusFork fork);
    public byte[] HashTreeRoot();                 // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### LightClientFinalityUpdate

Light client update proving finality without sync committee rotation.

```csharp
public class LightClientFinalityUpdate
{
    public ConsensusFork Fork { get; set; }
    public LightClientHeader AttestedHeader { get; set; }
    public LightClientHeader FinalizedHeader { get; set; }
    public IList<byte[]> FinalityBranch { get; set; }
    public SyncAggregate SyncAggregate { get; set; }
    public ulong SignatureSlot { get; set; }

    public byte[] Encode();                       // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static LightClientFinalityUpdate Decode(byte[] data, ConsensusFork fork);
    public byte[] HashTreeRoot();                 // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### LightClientOptimisticUpdate

Lightweight update for optimistic header tracking.

```csharp
public class LightClientOptimisticUpdate
{
    public ConsensusFork Fork { get; set; }
    public LightClientHeader AttestedHeader { get; set; }
    public SyncAggregate SyncAggregate { get; set; }
    public ulong SignatureSlot { get; set; }

    public byte[] Encode();                       // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static LightClientOptimisticUpdate Decode(byte[] data, ConsensusFork fork);
    public byte[] HashTreeRoot();                 // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### SyncAggregate

Aggregated sync committee participation bits and BLS signature (160 bytes fixed).

```csharp
public class SyncAggregate
{
    public byte[] SyncCommitteeBits { get; set; }         // 64 bytes (512 bits)
    public byte[] SyncCommitteeSignature { get; set; }    // 96 bytes

    public byte[] Encode();
    public static SyncAggregate Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
}
```

### ExecutionPayloadHeader

Post-merge execution layer header.

```csharp
public class ExecutionPayloadHeader
{
    public ConsensusFork Fork { get; set; }          // fork this header belongs to
    public byte[] ParentHash { get; set; }           // 32 bytes
    public byte[] FeeRecipient { get; set; }         // 20 bytes
    public byte[] StateRoot { get; set; }            // 32 bytes
    public byte[] ReceiptsRoot { get; set; }         // 32 bytes
    public byte[] LogsBloom { get; set; }            // 256 bytes
    public byte[] PrevRandao { get; set; }           // 32 bytes
    public ulong BlockNumber { get; set; }           // 8 bytes
    public ulong GasLimit { get; set; }              // 8 bytes
    public ulong GasUsed { get; set; }               // 8 bytes
    public ulong Timestamp { get; set; }             // 8 bytes
    public byte[] ExtraData { get; set; }            // Variable (max 32 bytes)
    public byte[] BaseFeePerGas { get; set; }        // 32 bytes (little-endian uint256)
    public byte[] BlockHash { get; set; }            // 32 bytes
    public byte[] TransactionsRoot { get; set; }     // 32 bytes
    public byte[] WithdrawalsRoot { get; set; }      // 32 bytes (Capella+)
    public ulong BlobGasUsed { get; set; }           // 8 bytes (Deneb+)
    public ulong ExcessBlobGas { get; set; }         // 8 bytes (Deneb+)

    public byte[] Encode();                          // uses this.Fork
    public byte[] Encode(ConsensusFork fork);
    public static ExecutionPayloadHeader Decode(ReadOnlySpan<byte> data, ConsensusFork fork);
    public byte[] HashTreeRoot();                    // uses this.Fork
    public byte[] HashTreeRoot(ConsensusFork fork);
}
```

### SszBasicTypes

Constants for consensus layer types.

```csharp
public static class SszBasicTypes
{
    public const int RootLength = 32;
    public const int PubKeyLength = 48;
    public const int SignatureLength = 96;
    public const int SyncCommitteeSize = 512;
    public const int BeaconBlockHeaderLength = 112;
}
```

### SszContainerEncoding (internal)

Internal helper for combining fixed and dynamic SSZ sections. Used internally by container `Encode()` methods.

```csharp
internal static class SszContainerEncoding
{
    public static byte[] Combine(
        byte[] fixedSection,
        params byte[][] dynamicSections);
}
```

## Related Packages

- **Nethereum.Ssz**: SSZ primitives (reader, writer, merkleizer)
- **Nethereum.Hex**: Hex encoding/decoding for displaying roots and hashes
- **Nethereum.Util**: Cryptographic utilities including SHA-256
- **Nethereum.Merkle**: Merkle tree implementations for proof verification
- **Nethereum.Model**: Execution layer types (transactions, blocks)

## Important Notes

### Consensus vs Execution Layer

**DO NOT mix consensus and execution serialization formats:**

```csharp
// WRONG - using RLP on consensus types
var header = new BeaconBlockHeader();
byte[] rlpEncoded = header.EncodeRLP(); // Does not exist!

// CORRECT - using SSZ on consensus types
byte[] sszEncoded = header.Encode();
```

### Fixed Size Requirements

Many fields have **strict size requirements**:

```csharp
// WRONG - incorrect sizes
header.ParentRoot = new byte[16]; // Must be 32!
syncCommittee.PubKeys.Add(new byte[64]); // Must be 48!

// CORRECT
header.ParentRoot = new byte[32];
syncCommittee.PubKeys.Add(new byte[48]);
```

### Hash Function

Consensus layer uses **SHA-256**, not Keccak-256:

```csharp
// WRONG - Keccak is for execution layer
byte[] hash = Keccak256.Compute(header.Encode());

// CORRECT - SHA-256 for consensus
byte[] root = header.HashTreeRoot();
```

### Sync Committee Size

Sync committees **must have exactly 512 validators**:

```csharp
// WRONG
var syncCommittee = new SyncCommittee
{
    PubKeys = new List<byte[]>(256) // Wrong size!
};

// CORRECT
var syncCommittee = new SyncCommittee
{
    PubKeys = new List<byte[]>(512)
};
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
{
    syncCommittee.PubKeys.Add(new byte[48]);
}
```

### Merkle Branch Depth

Merkle branch depth varies by **consensus spec version**. Always verify against the current Ethereum specification.

## Additional Resources

- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs)
- [Light Client Sync Protocol](https://github.com/ethereum/consensus-specs/tree/dev/specs/altair/light-client)
- [SSZ Specification](https://github.com/ethereum/consensus-specs/blob/dev/ssz/simple-serialize.md)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [Ethereum Beacon Chain](https://ethereum.org/en/roadmap/beacon-chain/)

## License

This package is part of the Nethereum project and follows the same MIT license.
