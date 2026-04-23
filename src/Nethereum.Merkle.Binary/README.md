# Nethereum.Merkle.Binary

Binary Merkle Trie implementation for Ethereum stateless execution per [EIP-7864](https://eips.ethereum.org/EIPS/eip-7864). Provides a stem-based binary trie with 256-value stem nodes, proof generation/verification, key derivation, and pluggable hash providers.

## Overview

EIP-7864 proposes replacing Ethereum's Patricia Merkle Trie with a binary trie structure that enables stateless block execution through smaller proofs. This library implements the complete specification:

- **Binary trie** with stems (31 bytes) and 256 colocated values per stem node
- **Key derivation** for account data, code chunks, and storage slots
- **BasicDataLeaf packing** — version, code size, nonce, and balance in a single 32-byte leaf
- **Code chunking** — split contract bytecode into 31-byte chunks with PUSH continuation tracking
- **Proof generation and verification** — compact Merkle proofs for stateless validation
- **Pluggable hashing** — SHA-256 (default) or BLAKE3

## Installation

```bash
dotnet add package Nethereum.Merkle.Binary
```

### Dependencies

- **Nethereum.Util** — Hash providers (`IHashProvider`), byte array utilities
- **Nethereum.Hex** — Hex string conversions

## Key Concepts

### Binary Trie vs Patricia Trie

| Aspect | Patricia Trie (current) | Binary Trie (EIP-7864) |
|--------|------------------------|----------------------|
| Branching | 16-way (hex nibbles) | 2-way (binary bits) |
| Key structure | Nibble path | 31-byte stem + 1-byte suffix |
| Values per node | 1 | 256 (colocated under stem) |
| Proof size | Larger (16 children per branch) | Smaller (2 children per branch) |
| Hashing | Keccak-256 | BLAKE3 or SHA-256 |
| Use case | Current Ethereum state | Stateless execution |

### Stem Structure

A 32-byte key is split into:
- **Stem** (bytes 0-30): Shared prefix identifying a group of 256 related values
- **Suffix** (byte 31): Index 0-255 within the stem node

Account basic data, code hash, inline storage, and code chunks for the same address share a stem, allowing efficient access patterns.

## Quick Start

```csharp
using Nethereum.Merkle.Binary;

var trie = new BinaryTrie();

// Put a value (32-byte key, 32-byte value)
var key = new byte[32];
key[31] = 0x01;
var value = new byte[32];
value[0] = 0xFF;

trie.Put(key, value);

// Get the value back
var retrieved = trie.Get(key);

// Compute the root hash
var root = trie.ComputeRoot();
```

## Usage Examples

### Example 1: Basic Trie Operations

```csharp
using Nethereum.Merkle.Binary;

var trie = new BinaryTrie();

// Insert
var key = new byte[32];
var value = new byte[32];
value[0] = 0xAA;
trie.Put(key, value);

// Retrieve
var result = trie.Get(key);  // returns value

// Delete (sets to zero)
trie.Delete(key);
var deleted = trie.Get(key);  // returns 32 zero bytes

// Root hash
var root = trie.ComputeRoot();
```

### Example 2: EIP-7864 Key Derivation

```csharp
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Hashing;
using System.Numerics;

var keyDerivation = new BinaryTreeKeyDerivation(new Blake3HashProvider());
var address = new byte[20];  // Ethereum address

// Account basic data key (nonce, balance, code size)
var basicDataKey = keyDerivation.GetTreeKeyForBasicData(address);

// Code hash key
var codeHashKey = keyDerivation.GetTreeKeyForCodeHash(address);

// Storage slot key
var storageKey = keyDerivation.GetTreeKeyForStorageSlot(address, BigInteger.Zero);

// Code chunk key
var codeChunkKey = keyDerivation.GetTreeKeyForCodeChunk(address, chunkId: 5);
```

### Example 3: Proof Generation and Verification

```csharp
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Proofs;

var trie = new BinaryTrie();

// Insert some data
var key = new byte[32];
key[0] = 0x01;
var value = new byte[32];
value[0] = 0xFF;
trie.Put(key, value);

// Generate proof
var prover = new BinaryTrieProver(trie);
var proof = prover.BuildProof(key);

// Verify proof (can be done without the full trie)
var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
var verified = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
// verified == value
```

### Example 4: BasicDataLeaf Packing

Pack account state into a single 32-byte leaf:

```csharp
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Util;

byte version = 1;
uint codeSize = 24576;
ulong nonce = 42;
EvmUInt256 balance = 1_000_000_000_000_000_000UL;  // 1 ETH in wei

// Pack into 32 bytes
var packed = BasicDataLeaf.Pack(version, codeSize, nonce, balance);

// Unpack
BasicDataLeaf.Unpack(packed, out var v, out var cs, out var n, out EvmUInt256 b);
// v == 1, cs == 24576, n == 42, b == 1_000_000_000_000_000_000

// balance is a 128-bit field in the leaf — Pack throws
// ArgumentOutOfRangeException if balance.U2 or balance.U3 is non-zero.
```

The `balance` parameter is `EvmUInt256`, not `BigInteger` — the leaf layout allocates **16 bytes** (128 bits) for balance, so values above `2^128 - 1` are rejected up front to preserve round-trip safety. `BigInteger` callers can pass directly via `EvmUInt256`'s implicit conversion.

**Leaf layout (32 bytes):**

| Offset | Size | Field |
|--------|------|-------|
| 0 | 1 byte | Version |
| 1-4 | 4 bytes | Reserved |
| 5-7 | 3 bytes | Code size (big-endian) |
| 8-15 | 8 bytes | Nonce (big-endian) |
| 16-31 | 16 bytes | Balance (big-endian) |

### Example 5: Code Chunking

```csharp
using Nethereum.Merkle.Binary.Keys;

// Split bytecode into 31-byte chunks
var code = new byte[] { 0x60, 0x80, 0x60, 0x40, 0x52 };  // PUSH1 0x80 PUSH1 0x40 MSTORE
var chunks = CodeChunker.ChunkifyCode(code);

// Each chunk is 32 bytes: [continuation_byte][31 bytes of code]
// continuation_byte tracks PUSH data spanning chunk boundaries
```

### Example 6: Custom Hash Provider

```csharp
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Util.HashProviders;

// SHA-256 (default)
var sha256Trie = new BinaryTrie(new Sha256HashProvider());

// BLAKE3 (faster, managed implementation)
var blake3Trie = new BinaryTrie(new Blake3HashProvider());

// Both produce deterministic roots given the same data
```

### Example 7: Stem Operations (Bulk Insert)

```csharp
using Nethereum.Merkle.Binary;

var trie = new BinaryTrie();

// Insert 256 values at a stem in one operation
var stem = new byte[31];
var values = new byte[256][];
values[0] = new byte[32]; values[0][0] = 0xAA;
values[255] = new byte[32]; values[255][0] = 0xCC;
// null entries are treated as zero

trie.PutStem(stem, values);

// Retrieve all values at a stem
var retrieved = trie.GetValuesAtStem(stem);
// retrieved[0][0] == 0xAA, retrieved[255][0] == 0xCC
```

### Example 8: Persistent Storage

```csharp
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Storage;

// Save trie to storage
var storage = new InMemoryBinaryTrieStorage();
trie.SaveToStorage(storage);

// Load with a node resolver for lazy loading
var options = new BinaryTrieOptions
{
    HashProvider = new Blake3HashProvider(),
    NodeResolver = (path, hash) => storage.Get(hash)
};
var loaded = new BinaryTrie(options);
```

## API Reference

### BinaryTrie

```csharp
public class BinaryTrie
{
    BinaryTrie();
    BinaryTrie(IHashProvider hashProvider);
    BinaryTrie(BinaryTrieOptions options);

    byte[] Get(byte[] key);                                         // 32-byte key
    void Put(byte[] key, byte[] value);                            // 32-byte key and value
    void Delete(byte[] key);                                       // Sets value to zero
    byte[][] GetValuesAtStem(byte[] stem);                         // 31-byte stem
    void PutStem(byte[] stem, byte[][] values);                    // Bulk insert 256 values
    void ApplyBatch(IEnumerable<KeyValuePair<byte[], byte[]>> entries);
    byte[] ComputeRoot();
    int GetHeight();
    BinaryTrie Copy();
    void SaveToStorage(IBinaryTrieStorage storage);

    IHashProvider HashProvider { get; }
}
```

### BinaryTreeKeyDerivation

```csharp
public class BinaryTreeKeyDerivation
{
    BinaryTreeKeyDerivation(IHashProvider hashProvider);

    byte[] GetTreeKey(byte[] address32, BigInteger treeIndex, byte subIndex);
    byte[] GetTreeKeyForBasicData(byte[] address);
    byte[] GetTreeKeyForCodeHash(byte[] address);
    byte[] GetTreeKeyForCodeChunk(byte[] address, ulong chunkId);
    byte[] GetTreeKeyForStorageSlot(byte[] address, BigInteger storageKey);

    static byte[] AddressTo32(byte[] address);
}
```

### BasicDataLeaf

```csharp
public static class BasicDataLeaf
{
    static byte[] Pack(byte version, uint codeSize, ulong nonce, BigInteger balance);
    static void Unpack(byte[] leaf, out byte version, out uint codeSize,
                       out ulong nonce, out BigInteger balance);
}
```

### CodeChunker

```csharp
public static class CodeChunker
{
    static byte[][] ChunkifyCode(byte[] code);  // Returns 32-byte chunks
}
```

### BinaryTrieProver / BinaryTrieProofVerifier

```csharp
public class BinaryTrieProver
{
    BinaryTrieProver(BinaryTrie trie);
    BinaryTrieProof BuildProof(byte[] key);
}

public class BinaryTrieProofVerifier
{
    BinaryTrieProofVerifier(IHashProvider hashProvider);
    byte[] VerifyProof(byte[] rootHash, byte[] key, BinaryTrieProof proof);
}

public class BinaryTrieProof
{
    byte[][] Nodes { get; set; }
}
```

### ValuesMerkleizer

```csharp
public static class ValuesMerkleizer
{
    static byte[] Merkleize(byte[][] values, IHashProvider hashProvider);
}
```

Merkleizes a 256-element sparse array of 32-byte values into a single root hash using an 8-level binary tree.

### Hash Providers

```csharp
// BLAKE3 (managed implementation, no native dependencies)
public class Blake3HashProvider : IHashProvider
{
    Blake3HashProvider();
    byte[] ComputeHash(byte[] data);
}

// SHA-256 (from Nethereum.Util)
public class Sha256HashProvider : IHashProvider
```

### Node Types

| Type | Description |
|------|-------------|
| `EmptyBinaryNode` | Singleton empty node, returns 32 zero bytes as hash |
| `StemBinaryNode` | Holds 31-byte stem + up to 256 values (sparse) |
| `InternalBinaryNode` | Binary branch with left/right children |
| `HashedBinaryNode` | Lazy-loaded placeholder resolved via `NodeResolver` |

### CompactBinaryNodeCodec

```csharp
public static class CompactBinaryNodeCodec
{
    static byte[] Encode(IBinaryNode node, IHashProvider hashProvider);
    static IBinaryNode Decode(byte[] data, int depth);
}
```

Encoding formats:
- **Stem node**: `[0x01][stem:31][bitmap:32][present values...]` — bitmap indicates which of 256 slots are populated
- **Internal node**: `[0x02][leftHash:32][rightHash:32]` — always 65 bytes
- **Empty node**: empty byte array

### Storage

```csharp
public interface IBinaryTrieStorage
{
    void Put(byte[] key, byte[] value);
    byte[] Get(byte[] key);
    void Delete(byte[] key);
}

public class InMemoryBinaryTrieStorage : IBinaryTrieStorage
```

## Differences from Nethereum.Merkle

| Aspect | Nethereum.Merkle | Nethereum.Merkle.Binary |
|--------|-----------------|------------------------|
| Structure | Standard/sparse Merkle trees | EIP-7864 binary trie with stems |
| Use case | Airdrops, whitelisting, ZK state | Stateless Ethereum execution |
| Key size | Variable | 32 bytes (31-byte stem + suffix) |
| Value size | Variable | 32 bytes |
| Values per node | 1 | 256 (colocated under stem) |
| Hashing | Keccak-256, Poseidon | BLAKE3, SHA-256 |
| Specification | Various | EIP-7864 |
| Proofs | OpenZeppelin-compatible | Binary inclusion proofs |

## Performance

Block production benchmarks comparing Patricia Merkle Trie (Keccak) vs Binary Trie with Blake3 and Poseidon (BN254 Montgomery) hash providers. All tests use simple ETH transfers with JIT warmup. Release build, .NET 10.

### In-Memory (100 blocks x 100 txs = 10,000 transactions)

| | Patricia | Binary (Blake3) | Binary (Poseidon) |
|---|---|---|---|
| Avg/block | 127ms | 145ms | 207ms |
| Ratio | 1.0x | 1.14x | 1.63x |

### SQLite (50 blocks x 50 txs = 2,500 transactions)

| | Patricia | Binary (Blake3) | Binary (Poseidon) |
|---|---|---|---|
| Avg/block | 109ms | 95ms | 136ms |
| Ratio | 1.0x | 0.87x | 1.25x |

### RocksDB (50 blocks x 50 txs = 2,500 transactions)

| | Patricia | Binary (Blake3) | Binary (Poseidon) |
|---|---|---|---|
| Avg/block | 69ms | 70ms | 113ms |
| Ratio | 1.0x | 1.01x | 1.64x |

Blake3 binary trie matches Patricia performance and is faster on SQLite due to simpler tree structure (binary vs hex branching, no RLP encoding). Poseidon adds 1.25-1.64x overhead depending on backend — the algebraic hash cost is offset by incremental hashing (only dirty paths recomputed) and Montgomery CIOS field arithmetic.

The tradeoff: Poseidon is ZK-native. State trie validation accounts for ~43% of total ZK proving cost with Keccak. Replacing it with Poseidon eliminates that cost entirely in the proving circuit.

## Related Packages

- **Nethereum.Util** — `IHashProvider` interface, `Sha256HashProvider`, byte utilities
- **Nethereum.Merkle** — Standard and sparse Merkle trees for airdrops, whitelisting, and ZK state
- **Nethereum.Hex** — Hex string conversions
