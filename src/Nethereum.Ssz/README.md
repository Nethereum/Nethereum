# Nethereum.Ssz

Minimal Simple Serialize (SSZ) primitives for Ethereum consensus layer serialization and Merkleization.

## Overview

Nethereum.Ssz provides a lightweight implementation of Simple Serialize (SSZ), the serialization format used by Ethereum's consensus layer (Beacon Chain, validators, light clients):

- **SszWriter**: Serialize primitive types and collections to SSZ format
- **SszReader**: Deserialize SSZ-encoded data
- **SszMerkleizer**: Compute SHA-256 Merkle roots (hash tree roots)
- **Element Readers**: Extensible type-specific deserialization

SSZ is different from RLP (used in execution layer):
- **Little-endian** encoding vs RLP's big-endian
- **Fixed-size types** known at compile time
- **SHA-256** for Merkle trees vs Keccak-256
- **Standardized across** all consensus clients

## Installation

```bash
dotnet add package Nethereum.Ssz
```

### Dependencies

**Nethereum Dependencies:**
- **Nethereum.Hex** - Hex encoding/decoding
- **Nethereum.Util** - Utility functions and byte operations

## Key Concepts

### What is SSZ?

**Simple Serialize (SSZ)** is the serialization standard for Ethereum's consensus layer:

- Designed for **efficiency** and **simplicity**
- Supports **Merkleization** (hash tree roots)
- Used for beacon chain blocks, attestations, and light client updates
- Little-endian encoding for all multi-byte integers

### SSZ vs RLP

| Feature | SSZ (Consensus Layer) | RLP (Execution Layer) |
|---------|----------------------|----------------------|
| Encoding | Little-endian | Big-endian (network) |
| Hash Function | SHA-256 | Keccak-256 |
| Type System | Strongly typed | Dynamic |
| Use Case | Consensus, light clients | Transactions, blocks |

### Basic Types

**Fixed-Size Types:**
- `boolean`: 1 byte (0x00 or 0x01)
- `uint8`, `uint16`, `uint32`, `uint64`: Little-endian integers
- `Bytes[N]`: Fixed-length byte array
- `Vector[N]`: Fixed-length homogeneous collection

**Variable-Size Types:**
- `List[N]`: Variable-length collection (max N elements)
- `Bytes`: Variable-length byte array

### Merkleization

SSZ defines **hash tree roots** for all types:
- Data is split into 32-byte **chunks**
- Chunks are organized into a Merkle tree
- Uses **SHA-256** (not Keccak-256)
- Root represents the entire data structure

## Quick Start

```csharp
using Nethereum.Ssz;

// Serialize data
using var writer = new SszWriter();
writer.WriteBoolean(true);
writer.WriteUInt64(42);
writer.WriteFixedBytes(new byte[32], 32);
byte[] encoded = writer.ToArray();

// Deserialize data
var reader = new SszReader(encoded);
bool flag = reader.ReadBoolean();
ulong value = reader.ReadUInt64();
byte[] hash = reader.ReadFixedBytes(32);
```

## Usage Examples

### Example 1: Writing and Reading Primitive Types

```csharp
using Nethereum.Ssz;

// Create fixed-size byte array
var fixedBytes = new byte[32];
fixedBytes[0] = 0xAA;
fixedBytes[31] = 0xBB;

// Write primitives
byte[] buffer;
using (var writer = new SszWriter())
{
    writer.WriteBoolean(true);           // 1 byte: 0x01
    writer.WriteUInt64(42);              // 8 bytes: little-endian
    writer.WriteFixedBytes(fixedBytes, 32);  // 32 bytes
    buffer = writer.ToArray();
}

// Read back
var reader = new SszReader(buffer);
bool boolValue = reader.ReadBoolean();     // true
ulong intValue = reader.ReadUInt64();      // 42
byte[] bytes = reader.ReadFixedBytes(32);  // original array

Assert.True(boolValue);
Assert.Equal((ulong)42, intValue);
Assert.Equal(fixedBytes, bytes);
```

*Source: tests/Nethereum.Ssz.Tests/SszWriterReaderTests.cs*

### Example 2: Variable-Length Byte Arrays

```csharp
using Nethereum.Ssz;

// Write variable-length bytes
byte[] buffer;
using (var writer = new SszWriter())
{
    var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

    // Length prefix (4 bytes) + data
    writer.WriteVariableBytes(data, maxLength: 100);
    buffer = writer.ToArray();
}

// Buffer contains: [0x05, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05]
//                  ^^^^^^^^^^^^^^^^^^^^^^^^^ length prefix (5 in little-endian)

// Read back
var reader = new SszReader(buffer);
byte[] result = reader.ReadVariableBytes(maxLength: 100);

Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, result);
```

*Source: tests/Nethereum.Ssz.Tests/SszWriterReaderTests.cs*

### Example 3: Writing and Reading Lists

```csharp
using Nethereum.Ssz;
using System.Collections.Generic;

// Write a list of uint64
byte[] buffer;
using (var writer = new SszWriter())
{
    var values = new List<ulong> { 10UL, 12UL, 99UL };

    writer.WriteList(
        values,
        (w, value) => w.WriteUInt64(value),  // Element writer
        maxLength: 100                        // Max list size
    );

    buffer = writer.ToArray();
}

// Read back (need to know count)
var reader = new SszReader(buffer);
ulong[] result = SszReader.ReadList<ulong>(ref reader, count: 3);

Assert.Equal(new[] { 10UL, 12UL, 99UL }, result);
```

*Source: tests/Nethereum.Ssz.Tests/SszWriterReaderTests.cs*

### Example 4: Writing Vectors (Fixed-Size Collections)

```csharp
using Nethereum.Ssz;
using System.Collections.Generic;

// Vector: fixed number of fixed-size elements
using var writer = new SszWriter();

var vector = new List<byte[]>
{
    Enumerable.Repeat((byte)0x11, 32).ToArray(),
    Enumerable.Repeat((byte)0x22, 32).ToArray(),
    Enumerable.Repeat((byte)0x33, 32).ToArray()
};

// Write vector (validates count matches expected)
writer.WriteVector(
    vector,
    elementSize: 32,
    expectedElementCount: 3
);

byte[] encoded = writer.ToArray();
// encoded.Length = 96 bytes (3 × 32)

// Read back
var reader = new SszReader(encoded);
byte[][] result = reader.ReadVector(elementCount: 3, elementSize: 32);

Assert.Equal(vector, result);
```

*Source: tests/Nethereum.Ssz.Tests/SszWriterReaderTests.cs and src/Nethereum.Ssz/SszReader.cs*

### Example 5: Computing Merkle Roots (Merkleization)

```csharp
using Nethereum.Ssz;
using System.Collections.Generic;
using System.Linq;

// Create two 32-byte chunks
var chunkA = Enumerable.Repeat((byte)0x11, 32).ToArray();
var chunkB = Enumerable.Repeat((byte)0x22, 32).ToArray();
var chunks = new List<byte[]> { chunkA, chunkB };

// Compute Merkle root
byte[] root = SszMerkleizer.Merkleize(chunks);

// root = SHA256(chunkA || chunkB)
// Result is a 32-byte hash

// Verify against manual SHA-256
using (var sha = System.Security.Cryptography.SHA256.Create())
{
    var concat = new byte[64];
    Buffer.BlockCopy(chunkA, 0, concat, 0, 32);
    Buffer.BlockCopy(chunkB, 0, concat, 32, 32);
    var expected = sha.ComputeHash(concat);

    Assert.Equal(expected, root);
}
```

*Source: tests/Nethereum.Ssz.Tests/SszMerkleizerTests.cs*

### Example 6: Chunkifying Data

```csharp
using Nethereum.Ssz;
using System.Linq;

// Data that doesn't align to 32-byte chunks
var data = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();

// Split into 32-byte chunks (pads last chunk with zeros)
var chunks = SszMerkleizer.Chunkify(data);

// Result: 2 chunks
// Chunk 0: bytes 0-31 (full)
// Chunk 1: bytes 32-39 + 24 zero bytes (padded)

Assert.Equal(2, chunks.Count);
Assert.Equal(32, chunks[0].Length);
Assert.Equal(32, chunks[1].Length);

// First chunk contains first 32 bytes
Assert.Equal(data.Take(32), chunks[0]);

// Second chunk contains remaining 8 bytes + padding
Assert.Equal(data.Skip(32), chunks[1].Take(8));
Assert.All(chunks[1].Skip(8), b => Assert.Equal(0, b));
```

*Source: tests/Nethereum.Ssz.Tests/SszMerkleizerTests.cs*

### Example 7: Hash Tree Root for Vectors

```csharp
using Nethereum.Ssz;
using System.Collections.Generic;

// Create chunks for a vector
var chunkA = new byte[32];
var chunkB = new byte[32];
chunkB[0] = 0x01;

var chunks = new List<byte[]> { chunkA, chunkB };

// Compute hash tree root with length mixed in
byte[] root = SszMerkleizer.HashTreeRootVector(chunks, length: 2);

// Different length = different root (even with same data)
byte[] differentRoot = SszMerkleizer.HashTreeRootVector(chunks, length: 1);

Assert.NotEqual(root, differentRoot);

// Hash tree root = MixInLength(Merkleize(chunks), length)
// Final hash includes both the data and its length
```

*Source: tests/Nethereum.Ssz.Tests/SszMerkleizerTests.cs*

### Example 8: Custom Element Readers

```csharp
using Nethereum.Ssz;

// Register a custom reader for a type
public class MyCustomType
{
    public ulong Value1 { get; set; }
    public ushort Value2 { get; set; }
}

public class MyCustomTypeReader : ISszElementReader<MyCustomType>
{
    public MyCustomType Read(ref SszReader reader)
    {
        return new MyCustomType
        {
            Value1 = reader.ReadUInt64(),
            Value2 = reader.ReadUInt16()
        };
    }
}

// Register the reader
SszElementReaderRegistry.Register(new MyCustomTypeReader());

// Now you can use it with ReadList
byte[] buffer = /* SSZ-encoded list */;
var reader = new SszReader(buffer);
MyCustomType[] items = SszReader.ReadList<MyCustomType>(ref reader, count: 5);
```

*Source: src/Nethereum.Ssz/SszElementReaderRegistry.cs*

### Example 9: Handling Fixed vs Variable Bytes

```csharp
using Nethereum.Ssz;

using var writer = new SszWriter();

// Fixed bytes: MUST be exact length
var fixedData = new byte[32];
writer.WriteFixedBytes(fixedData, expectedLength: 32);  // OK

// This throws ArgumentException (length mismatch)
var wrongSize = new byte[4];
// writer.WriteFixedBytes(wrongSize, expectedLength: 32);  // THROWS!

// Variable bytes: includes length prefix
var variableData = new byte[] { 0x01, 0x02, 0x03 };
writer.WriteVariableBytes(variableData);  // Writes: [0x03,0x00,0x00,0x00,0x01,0x02,0x03]

// Can enforce max length
var tooLong = new byte[100];
// writer.WriteVariableBytes(tooLong, maxLength: 50);  // THROWS!

byte[] encoded = writer.ToArray();
```

*Source: tests/Nethereum.Ssz.Tests/SszWriterReaderTests.cs*

## API Reference

### SszWriter

Serializes data to SSZ format.

**Constructor:**
```csharp
SszWriter()  // Creates writer with internal MemoryStream
```

**Primitive Methods:**
- `void WriteBoolean(bool value)`: Write boolean (1 byte)
- `void WriteUInt16(ushort value)`: Write 16-bit unsigned integer (little-endian)
- `void WriteUInt32(uint value)`: Write 32-bit unsigned integer (little-endian)
- `void WriteUInt64(ulong value)`: Write 64-bit unsigned integer (little-endian)

**Byte Array Methods:**
- `void WriteFixedBytes(ReadOnlySpan<byte> bytes, int expectedLength)`: Write fixed-size byte array (validates length)
- `void WriteVariableBytes(ReadOnlySpan<byte> bytes, ulong? maxLength = null)`: Write variable-size byte array (with length prefix)
- `void WriteBytes(ReadOnlySpan<byte> bytes)`: Write raw bytes (no length prefix)

**Collection Methods:**
- `void WriteList<T>(IList<T> items, Action<SszWriter, T> writeElement, ulong? maxLength = null)`: Write list with custom element writer
- `void WriteVector(IList<byte[]> items, int elementSize, int? expectedElementCount = null)`: Write fixed-size vector

**Output:**
- `byte[] ToArray()`: Get serialized bytes
- `void Dispose()`: Dispose underlying stream

### SszReader

Deserializes SSZ-encoded data.

**Constructor:**
```csharp
SszReader(ReadOnlySpan<byte> data)  // Creates reader over byte span (ref struct)
```

**Primitive Methods:**
- `bool ReadBoolean()`: Read boolean value
- `ushort ReadUInt16()`: Read 16-bit unsigned integer (little-endian)
- `uint ReadUInt32()`: Read 32-bit unsigned integer (little-endian)
- `ulong ReadUInt64()`: Read 64-bit unsigned integer (little-endian)

**Byte Array Methods:**
- `byte[] ReadFixedBytes(int length)`: Read fixed-size byte array
- `byte[] ReadVariableBytes(ulong? maxLength = null)`: Read variable-size byte array (reads length prefix first)

**Collection Methods:**
- `static T[] ReadList<T>(ref SszReader reader, int count)`: Read list using registered element reader
- `static T[] ReadList<T>(ref SszReader reader, int count, ISszElementReader<T> elementReader)`: Read list with custom element reader
- `byte[][] ReadVector(int elementCount, int elementSize)`: Read fixed-size vector

**Utility:**
- `byte[] ReadRemaining()`: Read all remaining bytes

### SszMerkleizer

Computes SHA-256 Merkle roots for SSZ types.

**Static Methods:**

**`byte[] Merkleize(IList<byte[]> chunks)`**
- Compute Merkle root from 32-byte chunks
- Pads to next power of 2 with zero chunks
- Returns 32-byte root hash
- Uses SHA-256 (not Keccak-256)

**`IList<byte[]> Chunkify(ReadOnlySpan<byte> data)`**
- Split data into 32-byte chunks
- Pads last chunk with zeros if needed
- Returns list of 32-byte arrays

**`byte[] HashTreeRootVector(IList<byte[]> chunks, ulong length)`**
- Compute hash tree root for vectors
- Mixes in length: `MixInLength(Merkleize(chunks), length)`
- Returns 32-byte root

**`byte[] HashTreeRootList(IList<byte[]> chunks, ulong length)`**
- Compute hash tree root for lists
- Same as HashTreeRootVector (SSZ spec)
- Returns 32-byte root

**`byte[] MixInLength(byte[] root, ulong length)`**
- Mix length into root hash
- Returns `SHA256(root || length_as_32_bytes)`
- Used for variable-size types

### Element Reader Registry

Registry for type-specific SSZ readers.

**ISszElementReader<T>**
```csharp
public interface ISszElementReader<T>
{
    T Read(ref SszReader reader);
}
```

**SszElementReaderRegistry**

**Static Methods:**
- `static void Register<T>(ISszElementReader<T> reader)`: Register reader for type T
- `static ISszElementReader<T> Get<T>()`: Get registered reader (throws if not found)

**Built-in Readers:**
- `UInt64Reader`: Registered by default for `ulong`

## Related Packages

### Used By (Consumers)

- **Nethereum.Consensus.Ssz** - Consensus-specific SSZ containers
- **Nethereum.Beaconchain** - Beacon chain API client
- **Nethereum.Consensus.LightClient** - Light client implementation
- **Consensus Layer Tools** - Validator, attestation handling

### Dependencies

- **Nethereum.Hex** - Hex encoding/decoding
- **Nethereum.Util** - Byte array utilities

## Important Notes

### Little-Endian Encoding

SSZ uses **little-endian** for all multi-byte integers:
```csharp
// uint32 value = 0x12345678
// SSZ encoding: [0x78, 0x56, 0x34, 0x12]
// RLP encoding: [0x12, 0x34, 0x56, 0x78] (different!)
```

Always use SSZ for consensus layer, RLP for execution layer.

### SHA-256 vs Keccak-256

SSZ Merkleization uses **SHA-256**:
- Consensus layer: SHA-256
- Execution layer: Keccak-256

Never mix the two!

### Fixed vs Variable-Size Types

**Fixed-Size (known at compile time):**
- Primitives: `bool`, `uint16`, `uint32`, `uint64`
- Fixed arrays: `Bytes[32]`
- Vectors: `Vector[T, N]`

**Variable-Size (length encoded):**
- Variable arrays: `Bytes`
- Lists: `List[T, N]`

Variable-size types require a 4-byte length prefix (little-endian `uint32`).

### Chunk Size

SSZ uses **32-byte chunks** for Merkleization:
- All data is divided into 32-byte segments
- Partial chunks are zero-padded
- Compatible with SHA-256 output size

### Max Length Validation

Always specify `maxLength` for variable-size types:
```csharp
writer.WriteVariableBytes(data, maxLength: 2048);
reader.ReadVariableBytes(maxLength: 2048);
```

Prevents denial-of-service attacks with oversized data.

### Performance Considerations

**SszReader is a ref struct:**
- Cannot be stored in fields or properties
- Cannot be used in async methods
- Optimized for stack allocation (zero heap allocs)

**SszWriter uses MemoryStream:**
- Dispose after use
- For small writes, consider reusing writers
- `ToArray()` allocates a copy

**Merkleization:**
- O(n) complexity for n chunks
- Pads to power of 2 (may create many zero chunks)
- SHA-256 is faster than Keccak-256

### Common Pitfalls

1. **Wrong Endianness**: SSZ is little-endian, RLP is big-endian
2. **Wrong Hash Function**: Use SHA-256 for SSZ, not Keccak-256
3. **Forgetting Disposal**: Always dispose `SszWriter`
4. **Missing Reader Registration**: Register custom readers before using `ReadList<T>`
5. **Length Mismatch**: Fixed bytes must be exact length
6. **Count Unknown**: Reader needs element count for lists

### Differences from RLP

| Feature | SSZ | RLP |
|---------|-----|-----|
| **Endianness** | Little-endian | Big-endian |
| **Type System** | Strongly typed | Weakly typed |
| **Hash Function** | SHA-256 | Keccak-256 |
| **Use Case** | Consensus layer | Execution layer |
| **Merkleization** | Built-in | Separate (Patricia trie) |
| **Variable Length** | 4-byte prefix | Prefix length varies |

### Security Considerations

**Length Limits:**
- Always validate against maximum lengths
- Prevents memory exhaustion attacks
- SSZ spec defines max sizes for all types

**Chunk Padding:**
- Zero-padding is safe (deterministic)
- Never trust non-zero padding
- Validate chunk sizes (must be 32 bytes)

**Type Safety:**
- Use strongly-typed readers
- Validate expected element counts
- Never assume buffer size without checking

## Additional Resources

### Ethereum SSZ Specification
- [SSZ Spec](https://github.com/ethereum/consensus-specs/blob/dev/ssz/simple-serialize.md) - Complete specification
- [Merkleization](https://github.com/ethereum/consensus-specs/blob/dev/ssz/simple-serialize.md#merkleization) - Hash tree root algorithm

### Consensus Layer
- [Ethereum Consensus Specs](https://github.com/ethereum/consensus-specs) - Beacon chain, validators, light clients
- [Light Client Sync Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/light-client/sync-protocol.md)

### Ethereum Documentation
- [Consensus Layer Overview](https://ethereum.org/en/developers/docs/consensus-mechanisms/pos/)
- [Beacon Chain Explained](https://ethereum.org/en/upgrades/beacon-chain/)

### Nethereum Documentation
- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
