# Nethereum.Merkle

Comprehensive Merkle tree implementations for Ethereum smart contract verification, airdrops, whitelisting, and large-scale state management.

## Overview

Nethereum.Merkle provides production-ready Merkle tree data structures optimized for blockchain use cases:

- **Standard Merkle Trees**: For airdrops, whitelisting, and contract verification
- **OpenZeppelin Compatible**: Interoperable with OpenZeppelin's JavaScript library and Solidity contracts
- **Incremental Trees**: Efficient updates without rebuilding the entire tree
- **Sparse Merkle Trees**: Handle millions of records with database-backed storage
- **Proof Generation & Verification**: Create and verify cryptographic proofs on-chain and off-chain

Use Merkle trees to efficiently verify membership in large datasets, enable token airdrops, implement whitelists, or manage scalable state commitments.

## Installation

```bash
dotnet add package Nethereum.Merkle
```

### Dependencies

**Nethereum Dependencies:**
- **Nethereum.ABI** - ABI encoding for struct-based Merkle leaves
- **Nethereum.Util** - Keccak-256 hashing and byte array utilities

## Key Concepts

### What is a Merkle Tree?

A Merkle tree is a cryptographic data structure that allows efficient verification of large datasets:

1. **Leaves**: Data elements (hashed)
2. **Branches**: Hashes of pairs of child nodes
3. **Root**: Single hash representing the entire dataset

**Key Property**: You can prove an element exists in the dataset by providing a small "proof" (log₂(n) hashes) instead of the entire dataset.

### Pairing Strategies

When combining hash pairs, Nethereum.Merkle supports:

- **Sorted Pairing** (`PairingConcatType.Sorted`): Hashes are ordered before concatenation (OpenZeppelin standard)
- **Normal Pairing** (`PairingConcatType.Normal`): Hashes concatenated in given order

### Use Cases

1. **Token Airdrops**: Distribute tokens to thousands of addresses efficiently
2. **Whitelisting**: Verify user eligibility on-chain with minimal gas
3. **State Commitments**: Compress large state into a single hash
4. **Fraud Proofs**: Prove invalid state transitions in layer 2 solutions
5. **NFT Metadata**: Prove authenticity of off-chain metadata

## Quick Start

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using System.Collections.Generic;

// Create a simple merkle tree with string addresses
var addresses = new List<string>
{
    "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5",
    "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7",
    "0xfa6179E49EE57a06391F218965b35B632F930472"
};

var merkleTree = new MerkleTree<string>(
    new Sha3KeccackHashProvider(),
    new HexStringByteArrayConvertor()
);
merkleTree.BuildTree(addresses);

// Get the root hash for your smart contract
var rootHash = merkleTree.Root.Hash.ToHex(true);

// Generate proof for an address
var proof = merkleTree.GetProof(addresses[0]);

// Verify proof
var isValid = merkleTree.VerifyProof(proof, addresses[0]);
```

## Usage Examples

### Example 1: Simple Merkle Tree with Character Data

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;

// Create a list of characters
var elements = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/="
    .ToCharArray()
    .ToList();

// Build the merkle tree
var merkleTree = new MerkleTree<char>(
    new Sha3KeccackHashProvider(),
    new ChartByteArrayConvertor()
);
merkleTree.BuildTree(elements);

// Get the root hash
var hexRoot = merkleTree.Root.Hash.ToHex(true);
// Result: "0xec0dffcb601ee38fa372bbf1d89ed16761db0a0b215480032b783f8c33230783"

// Generate proof for 'A'
var proofs = merkleTree.GetProof('A');

// Verify the proof
var isValid = merkleTree.VerifyProof(proofs, 'A');  // Returns true
```

*Source: `MerkleUnitTests.cs:13-36`*

### Example 2: OpenZeppelin-Compatible Merkle Tree for Airdrops

```csharp
using Nethereum.Merkle;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

// Define your airdrop recipient struct (must match Solidity struct)
[Struct("AirdropRecipient")]
public class AirdropRecipient
{
    [Parameter("address", 1)]
    public string User { get; set; }

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}

// Create recipients list
var recipients = new List<AirdropRecipient>
{
    new AirdropRecipient
    {
        User = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5",
        Amount = BigInteger.Parse("1000000000000000000")  // 1 token
    },
    new AirdropRecipient
    {
        User = "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7",
        Amount = BigInteger.Parse("2000000000000000000")  // 2 tokens
    },
    new AirdropRecipient
    {
        User = "0xfa6179E49EE57a06391F218965b35B632F930472",
        Amount = BigInteger.Parse("500000000000000000")   // 0.5 tokens
    }
};

// Build OpenZeppelin-compatible merkle tree
var merkleTree = new OpenZeppelinStandardMerkleTree<AirdropRecipient>();
merkleTree.BuildTree(recipients);

// Deploy your smart contract with this root
var rootHash = merkleTree.Root.Hash.ToHex(true);

// User wants to claim - generate their proof
var userToClaim = recipients[0];
var proof = merkleTree.GetProof(userToClaim);

// User submits proof + their data to claim() function on-chain
// Contract verifies using OpenZeppelin's MerkleProof.sol
```

*Source: `OpenZeppelinMerkleUnitTests.cs:14-75`*

### Example 3: Whitelist with Single Parameter

```csharp
using Nethereum.Merkle;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;

[Struct("WhitelistEntry")]
public class WhitelistEntry
{
    [Parameter("address", 1)]
    public string User { get; set; }
}

// Create whitelist
var whitelist = new List<WhitelistEntry>
{
    new WhitelistEntry { User = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5" },
    new WhitelistEntry { User = "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7" },
    new WhitelistEntry { User = "0xfa6179E49EE57a06391F218965b35B632F930472" },
    new WhitelistEntry { User = "0x1f9090aaE28b8a3dCeaDf281B0F12828e676c326" }
};

var merkleTree = new OpenZeppelinStandardMerkleTree<WhitelistEntry>();
merkleTree.BuildTree(whitelist);

// Store root hash in your smart contract constructor
var rootHash = merkleTree.Root.Hash.ToHex(true);

// User proves they're whitelisted
var userEntry = whitelist[0];
var proof = merkleTree.GetProof(userEntry);
var isWhitelisted = merkleTree.VerifyProof(proof, userEntry);  // true
```

*Source: `OpenZeppelinMerkleUnitTests.cs:30-48`*

### Example 4: Lean Incremental Merkle Tree (Efficient Updates)

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using System.Numerics;

// Create an incremental tree (efficient for frequent updates)
var tree = new LeanIncrementalMerkleTree<BigInteger>(
    new Sha3KeccackHashProvider(),
    new BigIntegerByteArrayConvertor(),
    PairingConcatType.Normal
);

// Insert leaves one at a time (tree updates incrementally)
tree.InsertLeaf(BigInteger.Parse("100"));
tree.InsertLeaf(BigInteger.Parse("200"));
tree.InsertLeaf(BigInteger.Parse("300"));

// Get current root
var root = tree.Root.ToHex(true);

// Insert many leaves at once
var newValues = new[] {
    BigInteger.Parse("400"),
    BigInteger.Parse("500")
};
tree.InsertMany(newValues);

// Update existing leaf
tree.Update(0, BigInteger.Parse("150"));  // Change first leaf from 100 to 150

// Check if value exists
var hasValue = tree.Has(BigInteger.Parse("200"));  // true
var index = tree.IndexOf(BigInteger.Parse("200"));  // 1

// Generate proof
var proof = tree.GenerateProof(1);  // Proof for index 1

// Verify proof
var isValid = tree.VerifyProof(proof, BigInteger.Parse("200"), tree.Root);

// Export tree (for storage or sharing)
var exported = tree.Export();  // JSON string

// Import tree later
var imported = LeanIncrementalMerkleTree<BigInteger>.Import(
    new Sha3KeccackHashProvider(),
    new BigIntegerByteArrayConvertor(),
    exported,
    s => BigInteger.Parse(s)  // Leaf mapper
);
```

*Source: `LeanIncrementalMerkleTree.cs:41-230`*

### Example 5: Sparse Merkle Tree for Large Datasets

```csharp
using Nethereum.Merkle.Sparse;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using System.Collections.Generic;
using System.Threading.Tasks;

// Create sparse merkle tree with in-memory storage
// (use DatabaseSparseMerkleTreeStorage for millions of records)
var storage = new InMemorySparseMerkleTreeStorage<string>();

var sparseTree = new SparseMerkleTree<string>(
    depth: 256,  // Tree depth (bits for key space)
    hashProvider: new Sha3KeccackHashProvider(),
    byteArrayConvertor: new HexStringByteArrayConvertor(),
    storage: storage
);

// Set leaves by key (async for database support)
await sparseTree.SetLeafAsync("key1", "value1");
await sparseTree.SetLeafAsync("key2", "value2");
await sparseTree.SetLeafAsync("key3", "value3");

// Batch update for performance (critical for processing blocks)
var updates = new Dictionary<string, string>
{
    ["key4"] = "value4",
    ["key5"] = "value5",
    ["key6"] = "value6"
};
await sparseTree.SetLeavesAsync(updates);

// Get root hash (cached for performance)
var root = await sparseTree.GetRootHashAsync();

// Get individual leaf
var value = await sparseTree.GetLeafAsync("key1");

// Get leaf count
var count = await sparseTree.GetLeafCountAsync();  // 6

// Clear all data
await sparseTree.ClearAsync();
```

*Source: `SparseMerkleTree.cs:32-273`*

### Example 6: Using MerkleDropMerkleTree for Token Airdrops

```csharp
using Nethereum.Merkle;
using System.Collections.Generic;
using System.Numerics;

// MerkleDropItem is pre-defined for airdrop scenarios
var airdropItems = new List<MerkleDropItem>
{
    new MerkleDropItem
    {
        Index = 0,
        Account = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5",
        Amount = BigInteger.Parse("1000000000000000000")
    },
    new MerkleDropItem
    {
        Index = 1,
        Account = "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7",
        Amount = BigInteger.Parse("2500000000000000000")
    },
    new MerkleDropItem
    {
        Index = 2,
        Account = "0xfa6179E49EE57a06391F218965b35B632F930472",
        Amount = BigInteger.Parse("750000000000000000")
    }
};

// Build airdrop merkle tree
var merkleDropTree = new MerkleDropMerkleTree();
merkleDropTree.BuildTree(airdropItems);

// Get root for smart contract
var root = merkleDropTree.Root.Hash.ToHex(true);

// Generate proof for a specific recipient
var proof = merkleDropTree.GetProof(airdropItems[0]);

// Recipient calls claim(proof, index, account, amount) on contract
```

*Source: `MerkleDropMerkleTree.cs:1-8`*

### Example 7: Custom Merkle Tree with Custom Data Types

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using System.Collections.Generic;
using System.Text;

// Create custom byte array convertor for your data type
public class MyDataConvertor : IByteArrayConvertor<MyCustomData>
{
    public byte[] ConvertToByteArray(MyCustomData value)
    {
        // Serialize your data type to bytes
        var json = JsonConvert.SerializeObject(value);
        return Encoding.UTF8.GetBytes(json);
    }
}

public class MyCustomData
{
    public string Name { get; set; }
    public int Value { get; set; }
}

// Create merkle tree with custom type
var items = new List<MyCustomData>
{
    new MyCustomData { Name = "Alice", Value = 100 },
    new MyCustomData { Name = "Bob", Value = 200 },
    new MyCustomData { Name = "Charlie", Value = 150 }
};

var merkleTree = new MerkleTree<MyCustomData>(
    new Sha3KeccackHashProvider(),
    new MyDataConvertor(),
    PairingConcatType.Sorted  // OpenZeppelin compatible
);

merkleTree.BuildTree(items);

// Get root
var root = merkleTree.Root.Hash.ToHex(true);

// Generate and verify proof
var proof = merkleTree.GetProof(items[0]);
var isValid = merkleTree.VerifyProof(proof, items[0]);
```

*Source: `MerkleTree.cs:20-127`*

### Example 8: Dynamically Adding Leaves to Existing Tree

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;

// Start with initial set
var addresses = new List<string>
{
    "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5",
    "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7"
};

var merkleTree = new MerkleTree<string>(
    new Sha3KeccackHashProvider(),
    new HexStringByteArrayConvertor()
);
merkleTree.BuildTree(addresses);

var initialRoot = merkleTree.Root.Hash.ToHex(true);

// Add single leaf (tree rebuilds)
merkleTree.InsertLeaf("0xfa6179E49EE57a06391F218965b35B632F930472");

var newRoot = merkleTree.Root.Hash.ToHex(true);
// Root has changed!

// Add multiple leaves at once
var newAddresses = new[]
{
    "0x1f9090aaE28b8a3dCeaDf281B0F12828e676c326",
    "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb"
};
merkleTree.InsertLeaves(newAddresses);

// Tree now has 5 leaves total
var finalRoot = merkleTree.Root.Hash.ToHex(true);
```

*Source: `MerkleTree.cs:81-99`*

### Example 9: Static Proof Verification (Without Building Tree)

```csharp
using Nethereum.Merkle;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;

// You have a proof from somewhere (API, database, etc.)
var proof = new List<byte[]>
{
    "0x1f675bff07515f5df96737194ea945c36c41e7b4fcef307b7cd4d0e602a69111".HexToByteArray(),
    "0xe62e1dfc08d58fd144947903447473a090c958fe34e2425d578237fcdf1ab5a4".HexToByteArray(),
    "0x1907ce7877ec74782a26c166b562bfbdd4c8d8833f98ad82ae9dc8e98db20093".HexToByteArray()
};

var rootHash = "0xec0dffcb601ee38fa372bbf1d89ed16761db0a0b215480032b783f8c33230783".HexToByteArray();
var itemHash = "0x3f4a1640bcca71e45d053d67ab9891fe44608f4db37cc45e5523588c76c79539".HexToByteArray();

// Verify without building the tree (static method)
var hashProvider = new Sha3KeccackHashProvider();
var isValid = MerkleTree<object>.VerifyProof(
    proof,
    rootHash,
    itemHash,
    hashProvider,
    PairingConcatType.Sorted
);

// Returns true if proof is valid
```

*Source: `MerkleTree.cs:129-137`*

## API Reference

### MerkleTree<T>

Generic Merkle tree implementation with pluggable hashing and serialization.

**Constructor:**
```csharp
MerkleTree(
    IHashProvider hashProvider,
    IByteArrayConvertor<T> byteArrayConvertor,
    PairingConcatType pairingConcatType = PairingConcatType.Sorted
)
```

**Key Properties:**
- `MerkleTreeNode Root`: The root node of the tree
- `List<MerkleTreeNode> Leaves`: All leaf nodes
- `List<List<MerkleTreeNode>> Layers`: All layers of the tree (for proof generation)

**Key Methods:**
- `void BuildTree(List<T> items)`: Build tree from items
- `void InsertLeaf(T item)`: Add single item and rebuild
- `void InsertLeaves(IEnumerable<T> items)`: Add multiple items and rebuild
- `List<byte[]> GetProof(T item)`: Generate proof for an item
- `List<byte[]> GetProof(int index)`: Generate proof by leaf index
- `List<byte[]> GetProof(byte[] hashLeaf)`: Generate proof by leaf hash
- `bool VerifyProof(IEnumerable<byte[]> proof, T item)`: Verify proof for item
- `bool VerifyProof(IEnumerable<byte[]> proof, byte[] itemHash)`: Verify proof for hash
- `static bool VerifyProof(proof, rootHash, itemHash, hashProvider, pairingType)`: Static verification

### OpenZeppelinStandardMerkleTree<T>

Merkle tree compatible with OpenZeppelin's JavaScript library and Solidity contracts.

```csharp
var tree = new OpenZeppelinStandardMerkleTree<TAbiStruct>();
```

- Inherits from `AbiStructSha3KeccackMerkleTree<T>`
- Uses sorted pairing (OpenZeppelin standard)
- Works with ABI-annotated structs (`[Struct]` and `[Parameter]` attributes)

**Requirements:**
- Type `T` must have `[Struct]` attribute
- Properties must have `[Parameter]` attributes with correct types and order

### AbiStructMerkleTree<T>

Merkle tree for ABI-encoded structs.

```csharp
var tree = new AbiStructMerkleTree<MyStruct>();
```

- Uses `AbiStructEncoderPackedByteConvertor<T>` for encoding
- Uses Keccak-256 (Sha3) hashing
- Sorted pairing by default

### MerkleDropMerkleTree

Specialized tree for token airdrops using `MerkleDropItem`.

```csharp
var tree = new MerkleDropMerkleTree();
```

**MerkleDropItem Properties:**
- `BigInteger Index`: Sequential index
- `string Account`: Recipient address
- `BigInteger Amount`: Token amount

### LeanIncrementalMerkleTree<T>

Efficient incremental tree optimized for frequent updates.

**Constructor:**
```csharp
LeanIncrementalMerkleTree(
    IHashProvider hashProvider,
    IByteArrayConvertor<T> byteArrayConvertor,
    PairingConcatType pairingType = PairingConcatType.Normal
)
```

**Key Properties:**
- `byte[] Root`: Current root hash (auto-updated)
- `List<T> Leaves`: Current leaves
- `int Size`: Number of leaves
- `int Depth`: Tree depth

**Key Methods:**
- `void InsertLeaf(T leaf)`: Add single leaf (incremental update)
- `void InsertMany(IEnumerable<T> leaves)`: Batch insert
- `void Update(int index, T newLeaf)`: Update existing leaf
- `void UpdateMany(int[] indices, T[] newLeaves)`: Batch update
- `bool Has(T leaf)`: Check if leaf exists
- `int IndexOf(T leaf)`: Find leaf index
- `MerkleProof GenerateProof(int leafIndex)`: Generate proof
- `bool VerifyProof(MerkleProof proof, T leaf, byte[] root)`: Verify proof
- `string Export(Func<byte[], string> formatter = null)`: Export to JSON
- `static Import(hashProvider, convertor, json, leafMapper, ...)`: Import from JSON

### SparseMerkleTree<T>

High-performance sparse tree for millions of records with pluggable storage.

**Constructor:**
```csharp
SparseMerkleTree(
    int depth,  // 1-256
    IHashProvider hashProvider,
    IByteArrayConvertor<T> byteArrayConvertor,
    ISparseMerkleTreeStorage<T> storage
)
```

**Key Properties:**
- `int Depth`: Tree depth (key space size)
- `string EmptyLeafHash`: Hash of empty leaf

**Key Methods:**
- `async Task SetLeafAsync(string key, T value)`: Set leaf value
- `async Task<T> GetLeafAsync(string key)`: Get leaf value
- `async Task<string> GetRootHashAsync()`: Get cached root hash
- `async Task SetLeavesAsync(Dictionary<string, T> updates)`: Batch update (optimized)
- `async Task<long> GetLeafCountAsync()`: Count non-empty leaves
- `async Task ClearAsync()`: Clear all data

**Synchronous Overloads:**
- `void SetLeaf(string key, T value)`
- `T GetLeaf(string key)`
- `string GetRootHash()`

**Storage Implementations:**
- `InMemorySparseMerkleTreeStorage<T>`: For testing/small datasets
- `DatabaseSparseMerkleTreeStorage<T>`: For production/large datasets (requires `ISparseMerkleRepository`)

### MerkleProof

Container for merkle proof data.

**Properties:**
- `List<byte[]> ProofNodes`: Hashes needed to verify membership

### MerkleTreeNode

Node in the merkle tree.

**Properties:**
- `byte[] Hash`: Node hash value

**Methods:**
- `bool Matches(byte[] hash)`: Check if hash matches
- `MerkleTreeNode Clone()`: Create copy

### Pairing Strategies

#### PairingConcatType Enum
- `Sorted`: Sort hashes before concatenating (OpenZeppelin standard)
- `Normal`: Concatenate in given order

#### IPairConcatStrategy
Interface for custom pairing strategies.

**Implementations:**
- `SortedPairConcatStrategy`: Lexicographic sorting
- `PairConcatStrategy`: Direct concatenation

**Factory:**
- `PairingConcatFactory.GetPairConcatStrategy(PairingConcatType)`: Get strategy instance

## Related Packages

### Used By (Consumers)

- **Nethereum.Contracts** - Uses merkle trees for airdrop contract deployments
- **Smart Contract Verification** - On-chain proof verification
- **Token Distribution** - ERC-20/ERC-721 airdrops
- **Whitelisting Systems** - NFT mints, presales

### Dependencies

- **Nethereum.ABI** - ABI encoding for struct-based leaves
- **Nethereum.Util** - Keccak-256 hashing, byte conversions

## Important Notes

### Gas Optimization

**On-Chain Verification:**
- Proof verification costs approximately `keccak256(32 bytes) * depth` gas
- For tree depth 20 (1M leaves): ~20 keccak operations
- Much cheaper than storing/checking entire list on-chain

**Best Practices:**
- Use sorted pairing for OpenZeppelin compatibility
- Keep tree balanced (number of leaves close to power of 2)
- For very large trees, consider sparse merkle trees

### OpenZeppelin Compatibility

To ensure compatibility with OpenZeppelin's `MerkleProof.sol`:

1. Use `OpenZeppelinStandardMerkleTree<T>`
2. Use sorted pairing (`PairingConcatType.Sorted`)
3. Struct fields must match Solidity struct exactly (order and types)
4. Use `keccak256(abi.encodePacked(...))` encoding

**Solidity Example:**
```solidity
function claim(bytes32[] calldata proof, address account, uint256 amount) external {
    bytes32 leaf = keccak256(abi.encodePacked(account, amount));
    require(MerkleProof.verify(proof, merkleRoot, leaf), "Invalid proof");
    // ... claim logic
}
```

### Performance Considerations

**Standard MerkleTree:**
- Building: O(n log n)
- Proof generation: O(log n)
- Proof verification: O(log n)
- Inserting leaves: O(n log n) (rebuilds tree)

**LeanIncrementalMerkleTree:**
- Insert single leaf: O(n) (linear scan to rebuild)
- Better for frequent reads, occasional writes
- Export/import for persistence

**SparseMerkleTree:**
- Set leaf: O(log n) with path invalidation optimization
- Batch updates: O(m log n) for m leaves
- Root computation: O(1) when cached, O(log n) when dirty
- Optimized for millions of records with database storage

**Recommendation:**
- **< 10K items**: Use `MerkleTree<T>` or `OpenZeppelinStandardMerkleTree<T>`
- **10K-100K items**: Use `LeanIncrementalMerkleTree<T>` for updates
- **> 100K items**: Use `SparseMerkleTree<T>` with database storage

### Tree Depth Calculation

For n leaves:
- Depth = ⌈log₂(n)⌉
- Proof size = depth × 32 bytes

Examples:
- 100 leaves: depth 7, proof 224 bytes
- 1,000 leaves: depth 10, proof 320 bytes
- 1,000,000 leaves: depth 20, proof 640 bytes

### Security Considerations

**Second Preimage Attacks:**
- Nethereum.Merkle mitigates by using different encoding for leaves vs branches
- Leaves are hashed once, branches hash the concatenation of children

**Collision Resistance:**
- Keccak-256 provides 128-bit collision resistance
- Sufficient for all practical Ethereum use cases

**Proof Validation:**
- Always verify proofs on-chain before taking action
- Store merkle root on-chain (in contract storage or as constant)
- Never trust client-provided roots

### Common Pitfalls

1. **Forgetting to Rebuild**: After `InsertLeaf()`, the tree is rebuilt automatically
2. **Wrong Pairing Type**: Use `Sorted` for OpenZeppelin compatibility
3. **ABI Encoding Mismatch**: Ensure C# struct matches Solidity struct exactly
4. **Index vs Hash**: `GetProof()` has overloads for item, index, or hash
5. **Sparse Tree Keys**: Keys must fit within the tree depth (depth 256 = 256-bit keys)

### Sparse Merkle Tree Storage

**In-Memory** (testing only):
```csharp
var storage = new InMemorySparseMerkleTreeStorage<string>();
```

**Database** (production):
```csharp
public class MyRepository : ISparseMerkleRepository
{
    // Implement database access
}

var storage = new DatabaseSparseMerkleTreeStorage<string>(
    new MyRepository(),
    new HexStringByteArrayConvertor()
);
```

Database storage is **critical** for:
- Persisting tree across restarts
- Handling millions of records
- Enabling horizontal scaling

## Additional Resources

### Ethereum & Merkle Trees
- [Merkle Trees on Ethereum.org](https://ethereum.org/en/developers/docs/data-structures-and-encoding/patricia-merkle-trie/)
- [OpenZeppelin Merkle Tree JavaScript Library](https://github.com/OpenZeppelin/merkle-tree)
- [OpenZeppelin MerkleProof.sol](https://docs.openzeppelin.com/contracts/4.x/api/utils#MerkleProof)

### Use Case Examples
- [Uniswap Merkle Distributor](https://github.com/Uniswap/merkle-distributor) - Token airdrop pattern
- [ENS Airdrop](https://ens.mirror.xyz/cfvfKRpQSPtZJjPQOprWqEeqv2rytE7tQkxDg6ht7Oo) - Real-world example

### Research Papers
- [Certificate Transparency (Merkle Trees in Practice)](https://certificate.transparency.dev/)
- [Sparse Merkle Trees](https://eprint.iacr.org/2016/683.pdf)

### Nethereum Documentation
- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
