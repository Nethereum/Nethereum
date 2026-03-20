---
name: binary-merkle-trie
description: Help users work with EIP-7864 Binary Merkle Tries for stateless Ethereum execution using Nethereum.Merkle.Binary (.NET). Use this skill whenever the user mentions binary trie, EIP-7864, stateless execution, stem nodes, binary Merkle, BasicDataLeaf, code chunking, BLAKE3, or Verkle-style trie structures in a C#/.NET context.
user-invocable: true
---

# Binary Merkle Trie (EIP-7864) — Nethereum.Merkle.Binary

## When to Use This

Use this skill when a user wants to:
- Implement or test EIP-7864 binary Merkle tries
- Derive trie keys for account data, storage slots, or code chunks
- Pack/unpack account state into BasicDataLeaf format
- Generate or verify binary trie inclusion proofs
- Chunk contract bytecode for trie storage

## Required Packages

```bash
dotnet add package Nethereum.Merkle.Binary
```

## Core Concept

EIP-7864 proposes replacing Ethereum's Patricia trie with a binary structure for smaller proofs and stateless block execution. A 32-byte key splits into a 31-byte **stem** (shared prefix) and 1-byte **suffix** (index 0-255). Account data, code, and storage for the same address share a stem, so a single proof path covers all related values.

## Basic Operations

```csharp
using Nethereum.Merkle.Binary;

var trie = new BinaryTrie();

var key = new byte[32];
key[31] = 0x01;
var value = new byte[32];
value[0] = 0xFF;

trie.Put(key, value);
var retrieved = trie.Get(key);
trie.Delete(key);  // Sets value to zero
var root = trie.ComputeRoot();
```

## Key Derivation

Map Ethereum addresses and storage slots to trie keys:

```csharp
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Hashing;
using System.Numerics;

var kd = new BinaryTreeKeyDerivation(new Blake3HashProvider());
var address = new byte[20];  // Ethereum address

var basicDataKey = kd.GetTreeKeyForBasicData(address);
var codeHashKey = kd.GetTreeKeyForCodeHash(address);
var storageKey = kd.GetTreeKeyForStorageSlot(address, new BigInteger(42));
var codeChunkKey = kd.GetTreeKeyForCodeChunk(address, chunkId: 0);
```

## Account State Packing (BasicDataLeaf)

Pack nonce, balance, code size, and version into a single 32-byte leaf:

```csharp
using Nethereum.Merkle.Binary.Keys;
using System.Numerics;

var leaf = BasicDataLeaf.Pack(
    version: 1,
    codeSize: 24576,
    nonce: 42,
    balance: BigInteger.Parse("1000000000000000000"));

BasicDataLeaf.Unpack(leaf, out var version, out var codeSize,
                      out var nonce, out var balance);
```

Layout: `[version:1][reserved:4][codeSize:3][nonce:8][balance:16]` = 32 bytes.

## Code Chunking

Split bytecode into 31-byte chunks with PUSH continuation tracking:

```csharp
var chunks = CodeChunker.ChunkifyCode(bytecode);
// Each chunk: [continuation_byte][31 bytes of code]
```

## Proof Generation and Verification

```csharp
using Nethereum.Merkle.Binary.Proofs;

var prover = new BinaryTrieProver(trie);
var proof = prover.BuildProof(key);

// Verify independently (only needs root hash, key, and proof)
var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
var verified = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
// Returns the value if valid, null otherwise
```

## Stem-Level Bulk Operations

Insert or retrieve all 256 values at a stem:

```csharp
var stem = new byte[31];
var values = new byte[256][];
values[0] = new byte[32]; values[0][0] = 0xAA;
values[255] = new byte[32]; values[255][0] = 0xCC;

trie.PutStem(stem, values);
var retrieved = trie.GetValuesAtStem(stem);
```

## Hash Providers

```csharp
// SHA-256 (default)
var sha256Trie = new BinaryTrie(new Sha256HashProvider());

// BLAKE3 (faster, managed implementation)
var blake3Trie = new BinaryTrie(new Blake3HashProvider());
```

## Key Types

| Type | Purpose |
|------|---------|
| `BinaryTrie` | Main trie — Put, Get, Delete, ComputeRoot |
| `BinaryTreeKeyDerivation` | Map addresses/slots to trie keys |
| `BasicDataLeaf` | Pack/unpack account state (version, nonce, balance, codeSize) |
| `CodeChunker` | Split bytecode into 31-byte chunks |
| `BinaryTrieProver` | Generate inclusion proofs |
| `BinaryTrieProofVerifier` | Verify proofs against a root hash |
| `Blake3HashProvider` | BLAKE3 managed hash implementation |

## Common Gotchas

- Keys must be exactly 32 bytes, values exactly 32 bytes
- `Delete` sets the value to zero bytes — it doesn't remove the key
- The default hash provider is SHA-256, not BLAKE3
- `GetTreeKeyForBasicData` accepts a 20-byte address (internally padded to 32)
- Stems are 31 bytes (first 31 bytes of the key), suffix is byte 31

For full documentation, see: https://docs.nethereum.com/docs/consensus-and-cryptography/guide-binary-merkle-trie
