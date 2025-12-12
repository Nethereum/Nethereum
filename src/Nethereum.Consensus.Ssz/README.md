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
    ParentRoot = new byte[32] { 0x01, 0x02, /* ... */ },
    StateRoot = new byte[32] { 0x03, 0x04, /* ... */ },
    BodyRoot = new byte[32] { 0x05, 0x06, /* ... */ }
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
syncCommittee.Pubkeys = new List<byte[]>();
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
{
    var pubkey = new byte[SszBasicTypes.PubKeyLength];
    pubkey[0] = (byte)(i % 256);
    pubkey[1] = (byte)(i / 256);
    syncCommittee.Pubkeys.Add(pubkey);
}

// Aggregate public key
syncCommittee.AggregatePubkey = new byte[SszBasicTypes.PubKeyLength];

// Encode (24,624 bytes total)
byte[] encoded = syncCommittee.Encode();
Console.WriteLine($"Size: {encoded.Length}"); // 24,624

// Decode
var decoded = SyncCommittee.Decode(encoded);
Assert.Equal(512, decoded.Pubkeys.Count);
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

// Bootstrap data for light client initialization
var bootstrap = new LightClientBootstrap();

// Current header
bootstrap.Header = new LightClientHeader
{
    Beacon = new BeaconBlockHeader
    {
        Slot = 5000000,
        ProposerIndex = 1000,
        ParentRoot = new byte[32],
        StateRoot = new byte[32],
        BodyRoot = new byte[32]
    }
};

// Current sync committee
bootstrap.CurrentSyncCommittee = new SyncCommittee
{
    Pubkeys = new List<byte[]>(),
    AggregatePubkey = new byte[48]
};

// Add 512 validator pubkeys
for (int i = 0; i < 512; i++)
{
    bootstrap.CurrentSyncCommittee.Pubkeys.Add(new byte[48]);
}

// Merkle branch for verification (depth varies by spec)
bootstrap.CurrentSyncCommitteeBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32]
};

// Encode for transmission
byte[] encoded = bootstrap.Encode();

// Light client can decode and verify
var decoded = LightClientBootstrap.Decode(encoded);
```

### Example 5: ExecutionPayloadHeader Handling

```csharp
using Nethereum.Consensus.Ssz;

// Post-merge blocks include execution layer data
var executionHeader = new ExecutionPayloadHeader
{
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
    BaseFeePerGas = System.Numerics.BigInteger.Parse("15000000000"),
    BlockHash = new byte[32],
    TransactionsRoot = new byte[32]
};

// Encode execution header
byte[] encoded = executionHeader.Encode();

// Decode
var decoded = ExecutionPayloadHeader.Decode(encoded);

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

// Execution layer header (post-merge)
lightClientHeader.Execution = new ExecutionPayloadHeader
{
    BlockNumber = 15537394,
    BlockHash = new byte[32],
    ParentHash = new byte[32],
    // ... other fields
};

// Merkle branch proving execution payload inclusion
lightClientHeader.ExecutionBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32]
};

// Encode complete header
byte[] encoded = lightClientHeader.Encode();

// Light client uses this to verify execution layer blocks
var decoded = LightClientHeader.Decode(encoded);
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

// Containers with dynamic fields use offsets
var bootstrap = new LightClientBootstrap
{
    Header = new LightClientHeader(),
    CurrentSyncCommittee = new SyncCommittee(),
    CurrentSyncCommitteeBranch = new List<byte[]> { new byte[32] }
};

// Dynamic section comes after fixed section
// Fixed section contains 4-byte offsets to dynamic data
byte[] dynamicEncoded = bootstrap.Encode();
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

// Step 1: Bootstrap light client
var bootstrap = new LightClientBootstrap();
bootstrap.Header = new LightClientHeader
{
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
    Pubkeys = new List<byte[]>(),
    AggregatePubkey = new byte[48]
};

for (int i = 0; i < 512; i++)
{
    bootstrap.CurrentSyncCommittee.Pubkeys.Add(new byte[48]);
}

bootstrap.CurrentSyncCommitteeBranch = new List<byte[]>
{
    new byte[32], new byte[32], new byte[32], new byte[32]
};

// Step 2: Encode and transmit
byte[] bootstrapData = bootstrap.Encode();
Console.WriteLine($"Bootstrap size: {bootstrapData.Length} bytes");

// Step 3: Light client receives and decodes
var decoded = LightClientBootstrap.Decode(bootstrapData);

// Step 4: Verify sync committee
byte[] syncCommitteeRoot = decoded.CurrentSyncCommittee.HashTreeRoot();
Console.WriteLine($"Sync committee root: {syncCommitteeRoot.ToHex(true)}");

// Step 5: Now light client can verify future updates using this committee
Console.WriteLine($"Light client synced to slot: {decoded.Header.Beacon.Slot}");
Console.WriteLine($"Sync committee size: {decoded.CurrentSyncCommittee.Pubkeys.Count}");
```

## API Reference

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
    public List<byte[]> Pubkeys { get; set; }      // 512 x 48 bytes
    public byte[] AggregatePubkey { get; set; }    // 48 bytes

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
    public BeaconBlockHeader Beacon { get; set; }
    public ExecutionPayloadHeader Execution { get; set; }
    public List<byte[]> ExecutionBranch { get; set; }

    public byte[] Encode();
    public static LightClientHeader Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
}
```

### LightClientBootstrap

Initial sync data for light clients.

```csharp
public class LightClientBootstrap
{
    public LightClientHeader Header { get; set; }
    public SyncCommittee CurrentSyncCommittee { get; set; }
    public List<byte[]> CurrentSyncCommitteeBranch { get; set; }

    public byte[] Encode();
    public static LightClientBootstrap Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
}
```

### ExecutionPayloadHeader

Post-merge execution layer header.

```csharp
public class ExecutionPayloadHeader
{
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
    public byte[] ExtraData { get; set; }            // Variable
    public BigInteger BaseFeePerGas { get; set; }    // 32 bytes
    public byte[] BlockHash { get; set; }            // 32 bytes
    public byte[] TransactionsRoot { get; set; }     // 32 bytes

    public byte[] Encode();
    public static ExecutionPayloadHeader Decode(ReadOnlySpan<byte> data);
    public byte[] HashTreeRoot();
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

### SszContainerEncoding

Helper for encoding containers with fixed and dynamic sections.

```csharp
public static class SszContainerEncoding
{
    public static byte[] CombineFixedAndDynamicSections(
        byte[] fixedSection,
        byte[] dynamicSection);
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
// ❌ WRONG - using RLP on consensus types
var header = new BeaconBlockHeader();
byte[] rlpEncoded = header.EncodeRLP(); // Does not exist!

// ✅ CORRECT - using SSZ on consensus types
byte[] sszEncoded = header.Encode();
```

### Fixed Size Requirements

Many fields have **strict size requirements**:

```csharp
// ❌ WRONG - incorrect sizes
header.ParentRoot = new byte[16]; // Must be 32!
syncCommittee.Pubkeys.Add(new byte[64]); // Must be 48!

// ✅ CORRECT
header.ParentRoot = new byte[32];
syncCommittee.Pubkeys.Add(new byte[48]);
```

### Hash Function

Consensus layer uses **SHA-256**, not Keccak-256:

```csharp
// ❌ WRONG - Keccak is for execution layer
byte[] hash = Keccak256.Compute(header.Encode());

// ✅ CORRECT - SHA-256 for consensus
byte[] root = header.HashTreeRoot();
```

### Sync Committee Size

Sync committees **must have exactly 512 validators**:

```csharp
// ❌ WRONG
var syncCommittee = new SyncCommittee
{
    Pubkeys = new List<byte[]>(256) // Wrong size!
};

// ✅ CORRECT
var syncCommittee = new SyncCommittee
{
    Pubkeys = new List<byte[]>(512)
};
for (int i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
{
    syncCommittee.Pubkeys.Add(new byte[48]);
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
