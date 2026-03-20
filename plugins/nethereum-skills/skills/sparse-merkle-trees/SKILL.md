---
name: sparse-merkle-trees
description: Help users build sparse Merkle trees with Poseidon or SHA-256 hashing for ZK circuits, privacy pools, and state commitments using Nethereum.Merkle (.NET). Use this skill whenever the user mentions sparse Merkle trees, SMT, Poseidon hashing, Celestia SMT, ZK-compatible state trees, nullifier sets, membership proofs, or PoseidonSmtHasher in a C#/.NET context.
user-invocable: true
---

# Sparse Merkle Trees — Nethereum.Merkle

## When to Use This

Use this skill when a user wants to:
- Build a sparse Merkle tree for ZK circuit inputs (Circom, Halo2, Noir)
- Use Poseidon hashing for circuit-friendly Merkle trees
- Build Celestia-compatible sparse Merkle trees
- Create membership or non-membership proofs for privacy pools or anonymous voting
- Persist large Merkle trees with lazy node loading

## Required Packages

```bash
dotnet add package Nethereum.Merkle
dotnet add package Nethereum.Util
```

## Core Concept

`SparseMerkleBinaryTree<T>` is a binary sparse Merkle tree where:
- Keys are converted to bit paths for tree traversal
- Leaves store value hashes at the key's path
- Empty subtrees have a fixed hash (no storage needed)
- Root hash is deterministic regardless of insertion order

The `ISmtHasher` interface controls hashing. Three built-in strategies:

| Hasher | Hash Function | Use Case |
|--------|--------------|----------|
| `PoseidonSmtHasher` | Poseidon (CircomT3 leaf, CircomT2 node) | ZK circuits |
| `CelestiaSmtHasher` | SHA-256 with domain prefixes | Celestia compatibility |
| `DefaultSmtHasher` | Any `IHashProvider` | Generic use |

## Poseidon SMT for ZK Circuits

The most common use case — a Poseidon-based tree whose root can be used directly as a public input in Circom proofs:

```csharp
using Nethereum.Merkle.Sparse;
using Nethereum.Util.ByteArrayConvertors;

var smt = new SparseMerkleBinaryTree<byte[]>(
    new PoseidonSmtHasher(),
    new ByteArrayToByteArrayConvertor(),
    new IdentitySmtKeyHasher(256));

smt.Put(key1, value1);
smt.Put(key2, value2);
var root = smt.ComputeRoot();  // Circom-compatible root hash

var value = smt.Get(key1);
smt.Delete(key1);
```

Poseidon hash details:
- **Leaf**: `Poseidon(key, value, 1)` using CircomT3 (3 inputs)
- **Node**: `Poseidon(left, right)` using CircomT2 (2 inputs)

## Celestia-Compatible SMT

```csharp
var smt = new SparseMerkleBinaryTree<byte[]>(
    new CelestiaSmtHasher(),
    new ByteArrayToByteArrayConvertor());

smt.Put(key, value);
var root = smt.ComputeRoot();
```

Hash formulas:
- **Leaf**: `SHA256(0x00 || path || SHA256(value))`
- **Node**: `SHA256(0x01 || leftHash || rightHash)`

## Persistent Storage (Async API)

For trees that survive process restarts:

```csharp
var storage = new InMemorySmtNodeStorage();
var smt = new SparseMerkleBinaryTree<byte[]>(
    new PoseidonSmtHasher(),
    new ByteArrayToByteArrayConvertor(),
    new IdentitySmtKeyHasher(256),
    storage: storage);

await smt.PutAsync(key1, value1);
await smt.PutAsync(key2, value2);
var root = await smt.ComputeRootAsync();
await smt.FlushAsync();  // Persist all nodes

// Later — reload from storage
var smt2 = new SparseMerkleBinaryTree<byte[]>(
    new PoseidonSmtHasher(),
    new ByteArrayToByteArrayConvertor(),
    new IdentitySmtKeyHasher(256),
    storage: storage);
await smt2.LoadRootAsync(root);  // Lazy-loads nodes on demand
```

## Batch Operations

```csharp
var entries = new Dictionary<byte[], byte[]>
{
    { key1, value1 },
    { key2, value2 },
    { key3, value3 }
};

smt.PutBatch(entries);           // Sync
await smt.PutBatchAsync(entries); // Async

Console.WriteLine($"Leaves: {smt.LeafCount}");
```

## Key Path Strategies

| Implementation | Description |
|---------------|-------------|
| `IdentitySmtKeyHasher(n)` | Key bits used directly as path, n-bit depth |
| `Sha256SmtKeyHasher` | `SHA256(key)` → 256-bit path |

## Node Serialization (SmtNodeCodec)

For custom storage backends:

```csharp
byte[] encoded = SmtNodeCodec.EncodeLeaf(path, valueBytes);
SmtNodeCodec.DecodeLeaf(encoded, out var path, out var value);

byte[] branch = SmtNodeCodec.EncodeBranch(leftHash, rightHash);
SmtNodeCodec.DecodeBranch(branch, 32, out var left, out var right);

bool isLeaf = SmtNodeCodec.IsLeaf(data);
bool isBranch = SmtNodeCodec.IsBranch(data);
```

## Common Gotchas

- The tree root is deterministic — insertion order doesn't matter
- `PoseidonSmtHasher` uses LSB-first bit ordering, `CelestiaSmtHasher` uses MSB-first
- `InMemorySmtNodeStorage` is thread-safe (`ConcurrentDictionary`) but for production use, implement `ISmtNodeStorage` with a database backend
- `IdentitySmtKeyHasher` requires keys to be the exact bit length specified — use `Sha256SmtKeyHasher` for variable-length keys

For full documentation, see: https://docs.nethereum.com/docs/consensus-and-cryptography/guide-sparse-merkle-zk
