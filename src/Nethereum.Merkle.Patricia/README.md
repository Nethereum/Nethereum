# Nethereum.Merkle.Patricia

Patricia Merkle Trie implementation for Ethereum state verification, proof generation, and cryptographic validation.

## Overview

Nethereum.Merkle.Patricia implements the Modified Merkle Patricia Trie, the core data structure used by Ethereum for:

- **State Storage**: Account balances, nonces, contract code, and storage
- **Proof Verification**: Validate account state, storage values, and transactions
- **Light Clients**: Verify data without downloading the entire blockchain
- **State Roots**: Compute cryptographic commitments to blockchain state

This package provides a complete implementation of the Ethereum Yellow Paper specification for Patricia tries, including proof generation and verification capabilities.

## Installation

```bash
dotnet add package Nethereum.Merkle.Patricia
```

### Dependencies

**Nethereum Dependencies:**
- **Nethereum.Model** - Account and transaction models
- **Nethereum.RLP** - RLP encoding/decoding for trie nodes

## Key Concepts

### What is a Patricia Merkle Trie?

A **Patricia Merkle Trie** (also called a "Merkle Patricia Tree") is a combination of:

1. **Patricia Trie**: Radix trie optimized for sparse data (compressed paths)
2. **Merkle Tree**: Cryptographic hash tree for efficient verification

**Key Properties:**
- Deterministic: Same data always produces same root hash
- Efficient proofs: Prove inclusion/exclusion in O(log n) space
- Optimized for sparse data: Common prefixes are compressed

### Ethereum Uses

Ethereum uses three Patricia tries per block:

1. **State Trie**: Mapping address → account (balance, nonce, storage root, code hash)
2. **Transaction Trie**: Mapping index → transaction data
3. **Receipt Trie**: Mapping index → transaction receipt (logs, status)

### Node Types

The Patricia trie uses four node types:

- **EmptyNode**: Represents absence of data
- **LeafNode**: Terminal node containing value and remaining path
- **ExtensionNode**: Compressed path of shared nibbles
- **BranchNode**: 16-way fork (one per hex digit) plus optional value

### Nibbles

Keys are split into **nibbles** (4-bit values, 0-F hex digits):
- Byte `0x12` → Nibbles `[1, 2]`
- Byte `0xAB` → Nibbles `[A, B]`

This allows efficient 16-way branching at each node.

## Quick Start

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using System.Text;

// Create a Patricia trie
var trie = new PatriciaTrie();

// Insert key-value pairs
trie.Put(new byte[] { 1, 2, 3, 4 }, Encoding.UTF8.GetBytes("monkey"));
trie.Put(new byte[] { 1, 2 }, Encoding.UTF8.GetBytes("giraffe"));

// Get the root hash (represents entire trie state)
var rootHash = trie.Root.GetHash().ToHex();

// Retrieve a value
var value = trie.Get(new byte[] { 1, 2 }, new InMemoryTrieStorage());
// value = "giraffe" (UTF-8 bytes)
```

## Usage Examples

### Example 1: Basic Patricia Trie Operations

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

// Create a trie
var trie = new PatriciaTrie();

// Insert values
trie.Put(new byte[] { 1, 2, 3, 4 }, new StringByteArrayConvertor().ConvertToByteArray("monkey"));
trie.Put(new byte[] { 1, 2 }, new StringByteArrayConvertor().ConvertToByteArray("giraffe"));

// Get root hash
var hash = trie.Root.GetHash();
// Result: "a02d89d1c0a595eecbcbee8b30c7c677be66b2314bc2661e163f1349868f45c7"

// Update an existing key
trie.Put(new byte[] { 1, 2 }, new StringByteArrayConvertor().ConvertToByteArray("elephant"));

// New root hash (changed!)
hash = trie.Root.GetHash();
// Result: "f249e880b1b8af8e788411e0cf26313cdfedb4388250f64ef10bea45ef76f9d1"
```

*Source: `PatriciaTrieTests.cs:13-34`*

### Example 2: Generating and Verifying Proofs

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;

// Build a trie with multiple values
var trie = new PatriciaTrie();
trie.Put(new byte[] { 1, 2, 3 }, new StringByteArrayConvertor().ConvertToByteArray("monkey"));
trie.Put(new byte[] { 1, 2, 3, 4, 5 }, new StringByteArrayConvertor().ConvertToByteArray("giraffe"));

// Generate proof for a specific key
var key = new byte[] { 1, 2, 3 };
var proofStorage = trie.GenerateProof(key);

// Proof is a collection of RLP-encoded nodes
// Now verify the proof with just the root hash (no need for full trie)

// Create a new trie from root hash only
var rootHash = trie.Root.GetHash();
var trie2 = new PatriciaTrie(rootHash, new Sha3KeccackHashProvider());

// Retrieve value using only the proof (minimal data)
var value = trie2.Get(key, proofStorage);

// Verify we got the correct value
Assert.Equal(
    new StringByteArrayConvertor().ConvertToByteArray("monkey").ToHex(),
    value.ToHex()
);

// Verify root hashes match
Assert.Equal(trie.Root.GetHash().ToHex(), trie2.Root.GetHash().ToHex());
```

*Source: `PatriciaTrieTests.cs:38-56`*

### Example 3: Account Proof Verification (Light Clients)

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using System.Collections.Generic;

// Scenario: Light client wants to verify account balance without full state

// State root from block header (trustworthy via PoS/PoW consensus)
var stateRoot = "0x1234...".HexToByteArray();

// Account to verify
var accountAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb";

// Proofs received from full node (via eth_getProof RPC)
var accountProofs = new List<byte[]>
{
    "0xf90211a0...".HexToByteArray(),  // RLP-encoded trie nodes
    "0xf90211a0...".HexToByteArray(),
    "0xf8518080...".HexToByteArray()
};

// Expected account state
var account = new Account
{
    Nonce = 42,
    Balance = BigInteger.Parse("5000000000000000000"),  // 5 ETH
    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
    CodeHash = DefaultValues.EMPTY_DATA_HASH
};

// Verify the proof
var isValid = AccountProofVerification.VerifyAccountProofs(
    accountAddress,
    stateRoot,
    accountProofs,
    account
);

if (isValid)
{
    // Account state is cryptographically verified!
    // We know the balance is correct without downloading entire state
    Console.WriteLine($"Account balance verified: {account.Balance} wei");
}
```

*Source: `AccountProofVerification.cs:15-36`*

### Example 4: Storage Proof Verification (Smart Contract Storage)

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;

// Scenario: Verify a specific storage slot value in a smart contract

// Storage slot key (e.g., slot 0 for a simple variable)
var storageKey = "0x0000000000000000000000000000000000000000000000000000000000000000".HexToByteArray();

// Expected value in that slot
var storageValue = "0x000000000000000000000000000000000000000000000000000000000000007B".HexToByteArray(); // 123 in hex

// Storage root from account (from account proof)
var storageRoot = "0xabcd...".HexToByteArray();

// Storage proofs from full node
var storageProofs = new List<byte[]>
{
    "0xf90211a0...".HexToByteArray(),
    "0xf87180a0...".HexToByteArray()
};

// Verify storage value
var isValid = StorageProofVerification.ValidateValueFromStorageProof(
    storageKey,
    storageValue,
    storageProofs,
    storageRoot
);

if (isValid)
{
    Console.WriteLine("Storage value verified: Contract's variable at slot 0 is 123");
}
```

*Source: `StorageProofVerification.cs:17-61`*

### Example 5: Transaction Trie Validation

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;
using System.Numerics;

// Scenario: Validate that transactions in a block match the transaction root

// Transactions from a block (with indices)
var transactions = new List<IndexedSignedTransaction>
{
    new IndexedSignedTransaction
    {
        Index = 0,
        SignedTransaction = new Transaction1559(
            chainId: 1,
            nonce: 0,
            maxPriorityFeePerGas: 2_000_000_000,
            maxFeePerGas: 100_000_000_000,
            gasLimit: 21000,
            receiverAddress: "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb",
            amount: BigInteger.Parse("1000000000000000000"),
            data: "",
            accessList: null,
            signature: new Signature(rBytes, sBytes, vBytes)
        )
    },
    new IndexedSignedTransaction
    {
        Index = 1,
        SignedTransaction = new LegacyTransaction(/* ... */)
    }
    // ... more transactions
};

// Transaction root from block header
var transactionsRoot = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421";

// Validate
var isValid = TransactionProofVerification.ValidateTransactions(
    transactionsRoot,
    transactions
);

if (isValid)
{
    Console.WriteLine("All transactions verified against block header!");
}
```

*Source: `TransactionProofVerification.cs:10-21`*

### Example 6: Building a Trie with Leaf Nodes

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

// Create trie
var trie = new PatriciaTrie();

// Insert a single key-value (creates a leaf node)
var key = new byte[] { 1, 2, 3, 4 };
var value = new StringByteArrayConvertor().ConvertToByteArray("monkey");
trie.Put(key, value);

// The root IS the leaf node at this point
// Manually create equivalent leaf node to verify structure
var leafNode = new LeafNode();
leafNode.Nibbles = key.ConvertToNibbles();  // [0, 1, 0, 2, 0, 3, 0, 4]
leafNode.Value = value;

// Verify hashes match
var trieHash = trie.Root.GetHash();
var leafNodeHash = leafNode.GetHash();

Assert.Equal(
    "f6ec9fe71a6649f422350f383ff0e2e33b42a2941b1c95599f145e1e3697b864",
    trieHash.ToHex()
);
Assert.Equal(trieHash.ToHex(), leafNodeHash.ToHex());
```

*Source: `PatriciaTrieTests.cs:59-72`*

### Example 7: Extension and Branch Node Creation

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;

// Start with a leaf
var trie = new PatriciaTrie();
var key1 = new byte[] { 1, 2, 3, 4 };
var value1 = new StringByteArrayConvertor().ConvertToByteArray("monkey");
trie.Put(key1, value1);

// Add a second key that shares a prefix
// This will create an ExtensionNode (shared prefix) + BranchNode (fork)
var key2 = new byte[] { 1, 2, 3 };
var value2 = new StringByteArrayConvertor().ConvertToByteArray("giraffe");
trie.Put(key2, value2);

// Resulting structure:
// ExtensionNode [0,1,0,2,0,3]
//   → BranchNode (value="giraffe")
//       → Child[0]: LeafNode [4] (value="monkey")

// Manually verify structure
var leafNode = new LeafNode
{
    Nibbles = new byte[] { 4 },  // Only the differing nibble
    Value = value1
};

var branchNode = new BranchNode();
branchNode.SetChild(0, leafNode);
branchNode.Value = value2;  // Branch can have a value!

var extendedNode = new ExtendedNode
{
    InnerNode = branchNode,
    Nibbles = key2.ConvertToNibbles()  // Shared prefix
};

// Verify structure matches
var trieHash = trie.Root.GetHash();
var extendedNodeHash = extendedNode.GetHash();

Assert.Equal(
    "3b8255bc1fb241a4e8eef2bebc2b783ad3aed8da7a5ceb06db39bda447be1531",
    trieHash.ToHex()
);
Assert.Equal(extendedNodeHash.ToHex(), trieHash.ToHex());
```

*Source: `PatriciaTrieTests.cs:75-107`*

### Example 8: Using Hash Nodes (Lazy Loading)

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Util.HashProviders;

// Hash nodes represent nodes not yet loaded into memory
// Used for efficient trie traversal with external storage

// Create a trie and populate it
var trie = new PatriciaTrie();
trie.Put(new byte[] { 1, 2, 3 }, "value1".ToBytes());
trie.Put(new byte[] { 1, 2, 4 }, "value2".ToBytes());
trie.Put(new byte[] { 5, 6, 7 }, "value3".ToBytes());

// Get root hash
var rootHash = trie.Root.GetHash();

// Store trie nodes in external storage
var storage = new InMemoryTrieStorage();
var rlpData = trie.Root.GetRLPEncodedData();
storage.Put(rootHash, rlpData);

// Later: Create trie from hash only (doesn't load full tree)
var trie2 = new PatriciaTrie(rootHash, new Sha3KeccackHashProvider());

// Root is initially a HashNode (not yet decoded)
Assert.IsType<HashNode>(trie2.Root);

// When we query, nodes are decoded on-demand
var value = trie2.Get(new byte[] { 1, 2, 3 }, storage);
// Hash node automatically decodes inner node during traversal
```

*Source: `PatriciaTrie.cs:62-78`*

### Example 9: Working with Nibbles

```csharp
using Nethereum.Merkle.Patricia;
using Nethereum.Hex.HexConvertors.Extensions;

// Understanding nibble conversion

// Byte array to nibbles
var bytes = new byte[] { 0x12, 0xAB, 0xCD };
var nibbles = bytes.ConvertToNibbles();
// Result: [1, 2, A, B, C, D] (12 nibbles total, 2 per byte)

// Nibbles are used as the path through the trie
// Each nibble (0-F) selects one of 16 branches in a BranchNode

// Example: Storing address 0x742d35Cc... in state trie
var address = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb".HexToByteArray();
var addressNibbles = address.ConvertToNibbles();
// addressNibbles = [7,4,2,d,3,5,C,c,6,6,3,4,C,0,5,3,2,9,2,5,a,3,b,8,4,4,B,c,9,e,7,5,9,5,f,0,b,E,b]

// Each nibble represents one step in the trie traversal
```

*Source: `NiblesBytesExtension.cs`*

## API Reference

### PatriciaTrie

Main Patricia Merkle Trie implementation.

**Constructors:**
```csharp
PatriciaTrie()
PatriciaTrie(IHashProvider hashProvider)
PatriciaTrie(byte[] hashRoot)
PatriciaTrie(byte[] hashRoot, IHashProvider hashProvider)
```

**Properties:**
- `Node Root`: Root node of the trie
- `IHashProvider HashProvider`: Hash provider (default: Keccak-256)

**Key Methods:**

**`void Put(byte[] key, byte[] value, ITrieStorage storage = null)`**
- Insert or update a key-value pair
- Automatically rebuilds affected nodes
- Time complexity: O(key length)

**`byte[] Get(byte[] key, ITrieStorage storage)`**
- Retrieve value for a key
- Returns `null` if key doesn't exist
- Requires storage for hash node resolution

**`InMemoryTrieStorage GenerateProof(byte[] key)`**
- Generate Merkle proof for a key
- Returns storage containing all nodes needed for verification
- Proof size: O(log n) where n = number of keys

### Proof Verification

Static classes for verifying proofs.

#### AccountProofVerification

**`static bool VerifyAccountProofs(string accountAddress, byte[] stateRoot, IEnumerable<byte[]> rlpProofs, Account account)`**
- Verify account state against state root
- Used by light clients to verify balances
- Parameters:
  - `accountAddress`: Ethereum address (hex string)
  - `stateRoot`: Block's state root hash
  - `rlpProofs`: RLP-encoded proof nodes (from `eth_getProof`)
  - `account`: Expected account state
- Returns: `true` if account state matches proof

#### StorageProofVerification

**`static bool ValidateValueFromStorageProof(byte[] key, byte[] value, IEnumerable<byte[]> proofs, byte[] stateRoot = null)`**
- Verify smart contract storage value
- Parameters:
  - `key`: Storage slot key
  - `value`: Expected storage value
  - `proofs`: RLP-encoded proof nodes
  - `stateRoot`: Storage root (from account)
- Returns: `true` if storage value matches proof

#### TransactionProofVerification

**`static bool ValidateTransactions(string transactionsRoot, List<IndexedSignedTransaction> transactions)`**
- Verify transactions match transaction root
- Rebuilds transaction trie and compares roots
- Parameters:
  - `transactionsRoot`: Transaction root from block header
  - `transactions`: List of indexed transactions
- Returns: `true` if reconstructed root matches

### Node Types

All nodes inherit from abstract `Node` class.

#### Node (Abstract Base)

**Properties:**
- `IHashProvider HashProvider`: Hash provider

**Methods:**
- `abstract byte[] GetRLPEncodedData()`: RLP encoding of node
- `virtual byte[] GetHash()`: Keccak-256 hash of RLP data

#### EmptyNode

Represents absence of a node (null placeholder).

#### LeafNode

Terminal node containing the final value.

**Properties:**
- `byte[] Nibbles`: Remaining path (nibbles)
- `byte[] Value`: Stored value

#### ExtensionNode

Represents compressed path of shared nibbles.

**Properties:**
- `byte[] Nibbles`: Shared path prefix
- `Node InnerNode`: Child node (branch or leaf)

#### BranchNode

16-way fork (one child per hex digit 0-F).

**Properties:**
- `Node[] Children`: 16 children (indexed 0-15)
- `byte[] Value`: Optional value (if key ends at branch)

**Methods:**
- `void SetChild(byte index, Node node)`: Set child at index (0-15)

#### HashNode

Placeholder for a node not yet loaded from storage.

**Properties:**
- `byte[] Hash`: Hash of the node
- `Node InnerNode`: Decoded node (lazy loaded)

**Methods:**
- `void DecodeInnerNode(ITrieStorage storage, bool decodeHashNodes)`: Load and decode from storage

### Storage Interfaces

#### ITrieStorage

Interface for trie node storage.

**Methods:**
- `byte[] Get(byte[] key)`: Retrieve node by hash
- `void Put(byte[] key, byte[] value)`: Store node

#### InMemoryTrieStorage

In-memory implementation of `ITrieStorage`.

**Usage:**
- For testing and proof generation
- For temporary trie operations
- Not suitable for large persistent tries

### Utility Classes

#### NodeDecoder

Decodes RLP-encoded nodes.

**Methods:**
- `Node DecodeNode(byte[] hash, bool decodeHashNodes, ITrieStorage storage)`: Decode node from storage

#### NiblesBytesExtension

Extension methods for nibble conversion.

**Methods:**
- `byte[] ConvertToNibbles(this byte[] bytes)`: Convert bytes to nibbles
- `byte[] FindAllTheSameBytesFromTheStart(this byte[] a, byte[] b)`: Find common prefix

## Related Packages

### Used By (Consumers)

- **Nethereum.RPC** - eth_getProof RPC methods return Patricia trie proofs
- **Light Clients** - Verify state without full blockchain
- **State Verification Tools** - Validate blockchain state integrity
- **Archive Nodes** - Serve historical state proofs

### Dependencies

- **Nethereum.Model** - Account and transaction models for verification
- **Nethereum.RLP** - RLP encoding for trie nodes

## Important Notes

### Ethereum State Trie

The Ethereum state trie maps:
```
keccak256(address) → RLP([nonce, balance, storageRoot, codeHash])
```

**Key Points:**
- Keys are hashed (prevents rainbow table attacks)
- Values are RLP-encoded account objects
- Storage root points to account's storage trie

### Storage Trie

Each contract has its own storage trie:
```
keccak256(slot) → RLP(value)
```

**Key Points:**
- Keys are hashed storage slots
- Values are RLP-encoded
- Root stored in account's `stateRoot` field

### Transaction and Receipt Tries

```
RLP(index) → RLP(transaction)
```

**Key Points:**
- Keys are transaction indices (0, 1, 2, ...)
- Not hashed (sequential access pattern)
- Rebuilt from transaction list

### Proof Size

Proof size depends on trie depth:
- Average depth: ~5-7 nodes
- Worst case: ~64 nodes (256-bit key / 4 bits per nibble)
- Each node: ~500-1500 bytes (RLP encoded)
- Total proof: ~2.5-100 KB typically

### Security Considerations

**Proof Verification:**
- Always verify against a trusted root hash
- Root hash comes from block header (validated by consensus)
- Never trust client-provided roots

**Hash Collisions:**
- Keccak-256 provides 128-bit collision resistance
- Sufficient for all practical blockchain use cases

**Denial of Service:**
- Deep tries can be expensive to traverse
- Use storage limits for untrusted tries
- Consider proof size limits

### Performance Optimization

**For Large Tries:**
- Use external storage (database) for production
- Implement `ITrieStorage` with persistent backend
- Cache frequently accessed nodes
- Use HashNodes for lazy loading

**For Proof Generation:**
- Only generate proofs for necessary keys
- Cache proofs if queried frequently
- Consider proof caching service

**Memory Management:**
- Use HashNodes to avoid loading entire trie
- Implement node eviction for memory-constrained environments
- Clear storage after proof verification

### Common Pitfalls

1. **Forgetting to Hash Keys**: State trie uses `keccak256(address)` as keys, not raw address
2. **Wrong Encoding**: Keys are RLP-encoded before nibble conversion
3. **Missing Storage**: `Get()` requires storage parameter for hash node resolution
4. **Null vs Empty**: EmptyNode vs null value - check both
5. **Nibble Confusion**: Remember keys are nibbles, not bytes (2 nibbles per byte)

### Differences from Standard Patricia Trie

Ethereum's Modified Merkle Patricia Trie differs from standard Patricia tries:

1. **Merkle Hashing**: Nodes are hashed (Merkle property)
2. **RLP Encoding**: All data is RLP-encoded
3. **Hexary**: 16-way branching instead of binary
4. **Hash Nodes**: Lazy loading support via hash references
5. **Compact Encoding**: Special encoding for nibble paths (with terminator flag)

## Additional Resources

### Ethereum Yellow Paper
- [Section 4.1: World State](https://ethereum.github.io/yellowpaper/paper.pdf) - State trie specification
- [Appendix D: Modified Merkle Patricia Trie](https://ethereum.github.io/yellowpaper/paper.pdf) - Complete trie specification

### Ethereum Documentation
- [Patricia Merkle Trie](https://ethereum.org/en/developers/docs/data-structures-and-encoding/patricia-merkle-trie/)
- [State and Storage Proofs](https://blog.ethereum.org/2015/11/15/merkling-in-ethereum)
- [eth_getProof RPC Method](https://eips.ethereum.org/EIPS/eip-1186)

### Research Papers
- [Merkle Patricia Trie Specification](https://github.com/ethereum/wiki/wiki/Patricia-Tree) - Ethereum Wiki

### Nethereum Documentation
- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)

### Light Client Resources
- [EIP-1186: RPC-Method to get Merkle Proofs](https://eips.ethereum.org/EIPS/eip-1186)
- [Light Client Protocol](https://github.com/ethereum/consensus-specs/blob/dev/specs/altair/sync-protocol.md)
