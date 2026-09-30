# Nethereum.Merkle

Merkle trees for smart-contract verification: prove that an address is on an airdrop list, a whitelist or a snapshot, without putting the list on-chain.

## What you can do with it

- **Build an airdrop or whitelist tree** from a list of recipients and get the root your contract stores.
- **Produce a proof** a claimant passes to `MerkleProof.verify` on-chain.
- **Interoperate with OpenZeppelin** — the same root and the same proofs as their JavaScript `StandardMerkleTree` and `MerkleProof.sol`.
- **Verify a proof off-chain**, with or without rebuilding the tree.
- **Update a tree without rebuilding it** — insert, update or batch-update leaves incrementally.
- **Append cheaply up to a fixed capacity** — a frontier tree keeps O(depth) memory however many leaves have gone in, but holds at most `1 << depth` of them and throws once full.
- **Commit a huge key space** — a sparse tree materialises only the occupied leaves, over in-memory or database storage.
- **Target a ZK circuit** — Poseidon or Celestia-compatible hashing, with the bit-order and empty-hash conventions those circuits expect.

> This is **not** the Ethereum state trie. For the Modified Merkle Patricia Trie use **`Nethereum.Merkle.Patricia`**; for the EIP-7864 binary state tree use **`Nethereum.Merkle.Binary`**.

## Quick start

Build a tree from a list, take the root, and produce a proof.

*From `SimpleMerkleTest` in `tests/Nethereum.Contracts.IntegrationTests/Trie/MerkleDrop/MerkleUnitTests.cs:37` (use case `merkle-tree`) — a tagged, passing test. Two classes named `MerkleUnitTests` exist in that project; the tagged one is the `Trie.MerkleDrop` namespace, under the `Trie/` folder.*

```csharp
var elements = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray().ToList();
var merkleTree = new MerkleTree<char>(new Sha3KeccackHashProvider(), new ChartByteArrayConvertor());
merkleTree.BuildTree(elements);
var hexRoot = merkleTree.Root.Hash.ToHex(true);
Assert.Equal("0xec0dffcb601ee38fa372bbf1d89ed16761db0a0b215480032b783f8c33230783", hexRoot);

var proofs = merkleTree.GetProof('A');
```

For an on-chain airdrop or whitelist you almost always want the OpenZeppelin-compatible tree instead, over an ABI struct that mirrors your Solidity one. `SingleParam` below stands for that struct — in the tests it is a one-field type declared alongside them (`tests/Nethereum.Contracts.IntegrationTests/Trie/MerkleDrop/OpenZeppelinMerkleUnitTests.cs:82-87`), and `items` is a `List<SingleParam>`:

```csharp
[Struct("SingleParam")]
public class SingleParam
{
    [Parameter("address", 1)]
    public string User { get; set; }
}

var merkleTree = new OpenZeppelinStandardMerkleTree<SingleParam>();
merkleTree.BuildTree(items);

var hexRoot = merkleTree.Root.Hash.ToHex(true);
```

## Entry points

**Start with `OpenZeppelinStandardMerkleTree<T>`** if the proof will be checked by a contract — it fixes the hashing and pairing conventions OpenZeppelin's `MerkleProof.sol` expects, and takes no constructor arguments. Use `MerkleTree<T>` when you control both ends.

| I want to… | Reach for |
|---|---|
| **Airdrop or whitelist, verified on-chain** | **`new OpenZeppelinStandardMerkleTree<T>()` → `BuildTree(items)` → `Root.Hash` / `GetProof(item)`** |
| A tree over my own data type | `new MerkleTree<T>(hashProvider, byteArrayConvertor)` |
| A ready-made `(address, amount)` airdrop leaf | `new MerkleDropMerkleTree()` with `MerkleDropItem` |
| Verify a proof without the tree | `MerkleTree<T>.VerifyProof(proof, rootHash, itemHash, hashProvider, pairingType)` |
| Add leaves without rebuilding | `new LeanIncrementalMerkleTree<T>(…)` → `InsertLeaf` / `Update` |
| Append-only, fixed depth, O(depth) memory | `new FrontierMerkleTree(depth, hashProvider)` → `Append(leafHash)` |
| Commit millions of keys | `new SparseMerkleTree<T>(depth, hashProvider, convertor, storage)` |
| Feed a ZK circuit | `new SparseMerkleBinaryTree<T>(new PoseidonSmtHasher(), …)` |

On `MerkleTree<T>` and its subclasses `Root` is a `MerkleTreeNode`, so the value your contract stores is `tree.Root.Hash`. The other three trees expose the root differently: `LeanIncrementalMerkleTree<T>.Root` and `FrontierMerkleTree.Root` are raw `byte[]`, `SparseMerkleTree<T>` has `GetRootHashAsync()`, and `SparseMerkleBinaryTree<T>` has `ComputeRoot()`.

## Installation

```bash
dotnet add package Nethereum.Merkle
```

## Key concepts

### Hashing and leaf serialisation are injected

`MerkleTree<T>` never assumes how your leaf becomes bytes or how bytes become a hash:

```csharp
MerkleTree(
    IHashProvider hashProvider,
    IByteArrayConvertor<T> byteArrayConvertor,
    PairingConcatType pairingConcatType = PairingConcatType.Sorted)
```

`IHashProvider` (`Nethereum.Util.HashProviders`) is a single method, `byte[] ComputeHash(byte[] data)`; `Sha3KeccackHashProvider` is the Keccak-256 one. `IByteArrayConvertor<T>` (`Nethereum.Util.ByteArrayConvertors`) is a **two-way** contract — implement both halves:

```csharp
byte[] ConvertToByteArray(T data);
T ConvertFromByteArray(byte[] data);
```

The convertors that ship in `Nethereum.Util.ByteArrayConvertors` are `StringByteArrayConvertor` (UTF-8), `HexToByteArrayConvertor` (hex string ⇄ bytes), `ChartByteArrayConvertor` (`char`), and `ByteArrayToByteArrayConvertor` (identity).

### Pairing strategies

When combining a pair of hashes, the order matters and must match the verifier on the other side:

| `PairingConcatType` | Strategy | Meaning |
|---|---|---|
| `Sorted` | `SortedPairConcatStrategy` | sort the two hashes lexicographically before concatenating — **the OpenZeppelin convention** |
| `Normal` | `PairConcatStrategy` | concatenate left‖right as given |

```csharp
public enum PairingConcatType
{
    Normal,
    Sorted
}

public interface IPairConcatStrategy
{
    byte[] Concat(byte[] left, byte[] right);
}
```

`PairingConcatFactory.GetPairConcatStrategy(PairingConcatType type)` returns the strategy for a given type.

Sorted pairing is why `MerkleTree<T>.GetProof` returns only sibling hashes and no direction bits — the verifier can re-sort. `LeanIncrementalMerkleTree<T>` defaults to `Normal` and therefore returns a `MerkleProof` carrying explicit `PathIndices`.

### Use cases

Token airdrops, whitelists / allowlists, state commitments, fraud proofs, and off-chain metadata attestation — anywhere a contract must check membership of a large set without storing it.

## Usage

Snippets marked *From …* are extracted verbatim from `[NethereumDocExample(DocSection.SmartContracts, …)]`-tagged passing tests under `tests/Nethereum.Contracts.IntegrationTests/Trie/`.

**They are fragments, not programs.** Extraction keeps the body of the test method, so a snippet may use the fixture's fields and constants (`_hashProvider`, `_convertor`, `Leaves`, `TreeSize`, `ExpectedRootAfterInsert`) or a test-only `[Struct]` type such as `SingleParam`, and it may end in `Assert`. Open the named test file for the setup before copying.

### Build a tree, prove a leaf, verify the proof

*From `Trie.MerkleDrop.MerkleUnitTests.SimpleMerkleTest` (use case `merkle-tree`) — `tests/Nethereum.Contracts.IntegrationTests/Trie/MerkleDrop/MerkleUnitTests.cs:37`.*

```csharp
var elements = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray().ToList();
var merkleTree = new MerkleTree<char>(new Sha3KeccackHashProvider(), new ChartByteArrayConvertor());
merkleTree.BuildTree(elements);
var hexRoot = merkleTree.Root.Hash.ToHex(true);
Assert.Equal("0xec0dffcb601ee38fa372bbf1d89ed16761db0a0b215480032b783f8c33230783", hexRoot);

var proofs = merkleTree.GetProof('A');
```

`Root` is a `MerkleTreeNode`, so the on-chain root is `merkleTree.Root.Hash`. `GetProof` has three overloads — by item (`T`), by leaf hash (`byte[]`), and by leaf index (`int`).

### OpenZeppelin-compatible whitelist over an ABI struct

`OpenZeppelinStandardMerkleTree<T>` is `AbiStructSha3KeccackMerkleTree<T>` under a name that says what it is compatible with — it adds nothing and takes no constructor arguments. Its leaf is `keccak(keccak(abi.encode(params)))`: the convertor hashes the ABI encoding, and `MerkleTree<T>` hashes the leaf again — matching OpenZeppelin's `StandardMerkleTree` and `MerkleProof.sol`.

`AbiStructSha3KeccackMerkleTree<T>` also **sorts the leaves by hash before building** (`AbiStructSha3KeccackMerkleTree.cs:15-19` overrides `InitialiseLeavesAndLayersAndBuildTree` to `leaves.Sort(new MerkleTreeNodeComparer())`), which is the other half of OpenZeppelin compatibility. Two consequences: the tree's leaf order is **not** the order you passed items in, so `Leaves[i]` and `GetProof(int index)` index the sorted order; and the root is independent of input order. `AbiStructMerkleTree<T>` does *not* sort — it is the same ABI leaf without that override (`AbiStructMerkleTree.cs:7-12`). `T` must carry `[Struct]` and its properties `[Parameter]`, exactly as the Solidity struct declares them.

*From `Trie.MerkleDrop.OpenZeppelinMerkleUnitTests.SingleParamMultipleItems` (use case `merkle-tree`) — `tests/Nethereum.Contracts.IntegrationTests/Trie/MerkleDrop/OpenZeppelinMerkleUnitTests.cs:32`. This class is duplicated in the same project too; the tagged copy is the one under `Trie/`.*

```csharp
var item1 = new SingleParam { User = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5" };
var item2 = new SingleParam { User = "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7" };
var item3 = new SingleParam { User = "0xfa6179E49EE57a06391F218965b35B632F930472" };
var item4 = new SingleParam { User = "0x1f9090aaE28b8a3dCeaDf281B0F12828e676c326" };

var items = new List<SingleParam> { item1, item2, item3, item4 };

var merkleTree = new OpenZeppelinStandardMerkleTree<SingleParam>();
merkleTree.BuildTree(items);

var hexRoot = merkleTree.Root.Hash.ToHex(true);
```

The root here is `0xa1b819a3413fb3120a911642ee7fbdb17572e844090a95a0848d07aa230eb702` — pinned by the test, and produced by OpenZeppelin's JS `StandardMerkleTree` for the same input. `MultiParam_MultipleItems` in the same file pins a two-field struct.

Send `merkleTree.Root.Hash` to your contract's constructor, and give each claimant `merkleTree.GetProof(theirItem)` to pass to `MerkleProof.verify`.

### Airdrops: `MerkleDropMerkleTree`

`MerkleDropMerkleTree : AbiStructMerkleTree<MerkleDropItem>` is the pre-wired airdrop tree. `MerkleDropItem` is a `[Struct("MerkleDropItem")]` with exactly two parameters:

```csharp
[Struct("MerkleDropItem")]
public class MerkleDropItem
{
    [Parameter("address", "address")]
    public string Address { get; set; }

    [Parameter("uint256", "amount", 2)]
    public BigInteger Amount { get; set; }
}
```

*From `Trie.MerkleDrop.MerkleUnitTests.MerkleDropTree_ProvesARecipientAgainstItsRoot` (use case `merkle-tree`) — `tests/Nethereum.Contracts.IntegrationTests/Trie/MerkleDrop/MerkleUnitTests.cs:15`.*

```csharp
var tree = new MerkleDropMerkleTree();
tree.BuildTree(items);

var root = tree.Root.Hash.ToHex(true);
var proof = tree.GetProof(items[0]);

Assert.StartsWith("0x", root);
Assert.NotEmpty(proof);
Assert.True(tree.VerifyProof(proof, items[0]));
```

There is no index field — the leaf is `(address, amount)`. If your contract's leaf includes an index or any other field, define your own `[Struct]` type and use `AbiStructMerkleTree<T>` (packed ABI encoding) or `AbiStructSha3KeccackMerkleTree<T>` (Keccak of the ABI encoding) instead.

### Incremental updates: `LeanIncrementalMerkleTree<T>`

*From `LeanIncrementalMerkleTreeBigIntTests.InsertLeaves_ShouldProduceExpectedRootAndDepth` (use case `incremental-merkle-tree`).* **`_hashProvider` in that fixture is a toy `SumHashProvider` that adds two bytes and returns one** (`tests/Nethereum.Contracts.IntegrationTests/Trie/LeanIMT/LeanIncrementalMerkleTreeBigIntTests.cs:17,36-47`), which is the only reason the last two assertions hold: the root is one byte long, and `ExpectedRootAfterInsert` is the arithmetic sum `((0+1)+(2+3))+4 = 10` (`:27`). Under a real `Sha3KeccackHashProvider` the root is 32 bytes and `Assert.Single` would fail — read those two lines as fixture arithmetic, not as tree behaviour.

```csharp
var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
foreach (var leaf in Leaves)
    tree.InsertLeaf(leaf);

Assert.Equal(TreeSize, tree.Size);
Assert.Equal((int)Math.Ceiling(Math.Log2(TreeSize)), tree.Depth);
Assert.Single(tree.Root);
Assert.Equal(ExpectedRootAfterInsert, tree.Root[0]);
```

The two assertions that *do* generalise are the first two: `Size` counts the leaves inserted, and `Depth` is `ceil(log2(Size))`. `Root` here is a raw `byte[]` — its length is whatever the hash provider returns — not a `MerkleTreeNode`; the lean tree is a different type, not a `MerkleTree<T>` subclass. Inserting does not rebuild the whole tree.

Proofs carry direction, because the lean tree pairs in `Normal` order:

*From `LeanIncrementalMerkleTreeBigIntTests.GenerateAndVerifyProof_ForEachLeaf_ShouldBeValid` (use case `incremental-merkle-tree`).*

```csharp
var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
tree.InsertMany(Leaves);
for (int i = 0; i < TreeSize; i++)
{
    var proof = tree.GenerateProof(i);
    Assert.True(tree.VerifyProof(proof, Leaves[i], tree.Root));
}
```

A tree can be serialised and rebuilt — `Export()` produces JSON, `Import(...)` takes it back with a mapper from the exported leaf string to `T`:

*From `LeanIncrementalMerkleTreeBigIntTests.ExportImport_RoundTripPreservesTree` (use case `incremental-merkle-tree`).*

```csharp
var tree = new LeanIncrementalMerkleTree<byte[]>(_hashProvider, _convertor);
tree.InsertMany(Leaves);
var json = tree.Export();
var imported = LeanIncrementalMerkleTree<byte[]>.Import(
    _hashProvider,
    _convertor,
    json,
    str => Convert.FromBase64String(str)
);
Assert.Equal(tree.Size, imported.Size);
Assert.Equal(tree.Depth, imported.Depth);
Assert.Equal(tree.Root[0], imported.Root[0]);
```

`Import` verifies the imported node set against the leaves by default (`verifyIntegrity: true`); `Import_WithMismatchedTreeData_ShouldThrow` is the test that pins that.

### Append-only: `FrontierMerkleTree`

`FrontierMerkleTree` is a fixed-depth append-only tree that keeps only the right-hand frontier — O(depth) memory regardless of how many leaves have been appended. It is the shape a Solidity deposit contract uses.

```csharp
public class FrontierMerkleTree
{
    public FrontierMerkleTree(
        int depth,
        IHashProvider hashProvider,
        PairingConcatType pairingConcatType = PairingConcatType.Sorted);

    public byte[] Root { get; }
    public int NextIndex { get; }
    public int Capacity { get; }

    public void Append(byte[] leafHash);

    public static bool VerifyProof(
        MerkleProof proof,
        byte[] root,
        byte[] leafHash,
        IHashProvider hashProvider,
        PairingConcatType pairingConcatType = PairingConcatType.Sorted);
}
```

`depth` must be 1–30, `Capacity` is `1 << depth`, and `Append` takes a **pre-hashed** leaf.

### Sparse trees over a large key space: `SparseMerkleTree<T>`

*From `SparseMerkleTreeNewTests.NewImplementation_SimpleTest_ShouldWork` (use case `sparse-merkle-tree`).*

```csharp
var storage = new InMemorySparseMerkleTreeStorage<string>();
var convertor = new StringByteArrayConvertor();
var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);

var emptyRoot = await tree.GetRootHashAsync();

await tree.SetLeafAsync("10", "test_value");
var newRoot = await tree.GetRootHashAsync();

Assert.NotEqual(emptyRoot, newRoot);

var leafCount = await tree.GetLeafCountAsync();
Assert.Equal(1, leafCount);
```

The API is **async only** — there are no synchronous `SetLeaf` / `GetLeaf` / `GetRootHash` methods, because the storage behind it may be a database. `SetLeavesAsync(Dictionary<string, T> keyValuePairs)` batches a block's worth of updates into one pass instead of recomputing the root per key.

Storage is `ISparseMerkleTreeStorage<T>`, with two implementations: `InMemorySparseMerkleTreeStorage<T>` for tests and small sets, and `DatabaseSparseMerkleTreeStorage<T>` for production, whose single constructor parameter is an `ISparseMerkleRepository<T>` you implement against your database. (Both the repository and the storage are generic in the leaf type `T`.)

### ZK-oriented sparse tree: `SparseMerkleBinaryTree<T>`

`SparseMerkleBinaryTree<T>` is the binary sparse tree used for zero-knowledge circuits. It takes the hash *strategy* rather than a plain hash provider, because a ZK-friendly SMT differs in bit order, empty-hash handling and single-leaf collapsing:

```csharp
SparseMerkleBinaryTree(
    ISmtHasher hasher,
    IByteArrayConvertor<T> valueConvertor,
    ISmtKeyHasher keyHasher = null,      // defaults to IdentitySmtKeyHasher(256)
    ISmtNodeStorage storage = null)
```

| Sync | Async |
|---|---|
| `Put(byte[] key, T value)` | `PutAsync` |
| `Get(byte[] key)` | `GetAsync` |
| `Delete(byte[] key)` | `DeleteAsync` |
| `ComputeRoot()` | `ComputeRootAsync` |
| `PutBatch(IEnumerable<KeyValuePair<byte[], T>> entries)` | `PutBatchAsync` |

Three more members have no counterpart in the other column and are **not** async pairs of anything: `void Clear()` (`SparseMerkleBinaryTree.cs:75`) drops the in-memory tree and has no `ClearAsync`; `Task FlushAsync()` (`:126`) writes dirty nodes back to `ISmtNodeStorage`; `Task LoadRootAsync(byte[] rootHash)` (`:132`) attaches to a stored root and lazy-loads nodes as the walk needs them.

Plus `Depth`, `EmptyLeafHash` and `LeafCount`.

#### `ISmtHasher` — hashing strategies

```csharp
public interface ISmtHasher
{
    bool MsbFirst { get; }
    bool UseFixedEmptyHash { get; }
    bool CollapseSingleLeaf { get; }
    byte[] EmptyLeaf { get; }
    byte[] HashLeaf(byte[] path, byte[] valueBytes);
    byte[] HashNode(byte[] leftHash, byte[] rightHash);
}
```

| Hasher | `MsbFirst` | `UseFixedEmptyHash` | `CollapseSingleLeaf` | Use case |
|---|---|---|---|---|
| `PoseidonSmtHasher` | `false` | `true` | `true` | ZK circuits (Circom, Privacy Pools). `PoseidonSmtHasher(PoseidonHasher leafHasher, PoseidonHasher nodeHasher)` swaps the permutations. |
| `CelestiaSmtHasher` | `true` | `true` | `true` | Celestia sparse-Merkle-tree compatibility |
| `DefaultSmtHasher(IHashProvider hashProvider)` | `false` | `false` | `false` | generic — Keccak, SHA-256, anything |

#### `ISmtKeyHasher` — key → path

```csharp
public interface ISmtKeyHasher
{
    byte[] ComputePath(byte[] key);
    int PathBitLength { get; }
}
```

`IdentitySmtKeyHasher(int bitLength)` uses the key itself as the path; `Sha256SmtKeyHasher` hashes it to a 256-bit path. The tree's depth *is* `PathBitLength`, and must be 1–256.

#### `ISmtNodeStorage` and `SmtNodeCodec`

```csharp
public interface ISmtNodeStorage
{
    Task<byte[]> GetAsync(byte[] hash);
    Task PutAsync(byte[] hash, byte[] data);
    Task DeleteAsync(byte[] hash);
}
```

`InMemorySmtNodeStorage` is the in-box implementation, and `SmtNodeCodec` is the wire format those nodes are stored in:

```csharp
public static class SmtNodeCodec
{
    public static byte[] EncodeLeaf(byte[] path, byte[] valueBytes);
    public static void DecodeLeaf(byte[] data, out byte[] path, out byte[] valueBytes);

    public static byte[] EncodeBranch(byte[] leftHash, byte[] rightHash);
    public static void DecodeBranch(byte[] data, int hashSize, out byte[] leftHash, out byte[] rightHash);

    public static bool IsLeaf(byte[] data);
    public static bool IsBranch(byte[] data);
}
```

## API reference

### `MerkleTree<T>`

| Member | |
|---|---|
| `MerkleTree(IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, PairingConcatType pairingConcatType = PairingConcatType.Sorted)` | constructor |
| `MerkleTreeNode Root` | root node |
| `List<MerkleTreeNode> Leaves` | leaf nodes |
| `List<List<MerkleTreeNode>> Layers` | every layer, bottom-up |
| `void BuildTree(List<T> items)` | build from items |
| `MerkleTreeNode BuildTree(List<MerkleTreeNode> nodes)` | build from pre-hashed nodes; returns the root |
| `void InsertLeaf(T item)` / `void InsertLeaves(IEnumerable<T> items)` | add and rebuild (both `virtual`) |
| `List<byte[]> GetProof(T item)` / `GetProof(byte[] hashLeaf)` / `GetProof(int index)` | sibling hashes, leaf-to-root |
| `bool VerifyProof(IEnumerable<byte[]> proof, T item)` / `VerifyProof(IEnumerable<byte[]> proof, byte[] itemHash)` | verify against this tree's root |
| `static bool VerifyProof(IEnumerable<byte[]> proof, byte[] rootHash, byte[] itemHash, IHashProvider hashProvider, PairingConcatType pairingConcatType = PairingConcatType.Sorted)` | verify without a tree |
| `static byte[] ConcatAndHashPair(byte[] left, byte[] right, IHashProvider hashProvider, PairingConcatType pairingConcatType = PairingConcatType.Sorted)` | one pairing step |

Subclasses: `AbiStructMerkleTree<T>` (packed ABI encoding, Keccak, sorted), `AbiStructSha3KeccackMerkleTree<T>` (Keccak-of-ABI leaf hashing, Keccak, sorted), `OpenZeppelinStandardMerkleTree<T> : AbiStructSha3KeccackMerkleTree<T>`, `MerkleDropMerkleTree : AbiStructMerkleTree<MerkleDropItem>`. All four are parameterless.

### `LeanIncrementalMerkleTree<T>`

| Member | |
|---|---|
| `LeanIncrementalMerkleTree(IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, PairingConcatType pairingConcatType = PairingConcatType.Normal, bool hashLeafOnInsert = true, ILeanIMTNodeStorage storage = null)` | constructor |
| `byte[] Root` | current root (raw bytes) |
| `IReadOnlyList<T> Leaves`, `int Size`, `int Depth` | state |
| `ILeanIMTNodeStorage Storage` | the node store in use |
| `void InsertLeaf(T leaf)` / `void InsertMany(IEnumerable<T> leaves)` | append |
| `void Update(int index, T newLeaf)` / `void UpdateMany(int[] indices, T[] newLeaves)` | in-place update |
| `bool Has(T leaf)` / `int IndexOf(T leaf)` | lookup |
| `MerkleProof GenerateProof(int leafIndex)` / `bool VerifyProof(MerkleProof proof, T leaf, byte[] root)` | proofs |
| `string Export(Func<byte[], string> nodeFormatter = null)` | JSON |
| `static LeanIncrementalMerkleTree<T> Import(IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, string json, Func<string, T> leafMapper, Func<string, byte[]> nodeParser = null, PairingConcatType pairingConcatType = PairingConcatType.Normal, bool hashLeafOnInsert = true, ILeanIMTNodeStorage storage = null, bool verifyIntegrity = true)` | rebuild from JSON |
| `void VerifyStorageIntegrity()` | re-derive every node and throw on mismatch |

Node storage is pluggable, and defaults to `InMemoryLeanIMTNodeStorage`:

```csharp
public interface ILeanIMTNodeStorage
{
    byte[] GetNode(int level, int index);
    void SetNode(int level, int index, byte[] value);
    void SetNodesBatch(IEnumerable<LeanIMTNodeEntry> nodes);

    int GetNodeCount(int level);
    void EnsureLevel(int level);
    int GetLevelCount();

    void Clear();
}

public class LeanIMTNodeEntry
{
    public int Level { get; set; }
    public int Index { get; set; }
    public byte[] Value { get; set; }

    public LeanIMTNodeEntry(int level, int index, byte[] value);
}
```

### `SparseMerkleTree<T>`

| Member | |
|---|---|
| `SparseMerkleTree(int depth, IHashProvider hashProvider, IByteArrayConvertor<T> byteArrayConvertor, ISparseMerkleTreeStorage<T> storage)` and the overload adding `ISmtHasher hasher` | constructors; `depth` 1–256 |
| `int Depth`, `string EmptyLeafHash`, `IHashProvider HashProvider` | state |
| `Task SetLeafAsync(string key, T value)` / `Task<T> GetLeafAsync(string key)` | leaf access |
| `Task SetLeavesAsync(Dictionary<string, T> keyValuePairs)` | batch update |
| `Task<string> GetRootHashAsync()` | cached root |
| `Task<long> GetLeafCountAsync()` / `Task ClearAsync()` | maintenance |

### Supporting types

```csharp
public class MerkleProof
{
    public List<byte[]> ProofNodes { get; set; }
    public List<int> PathIndices { get; set; }
}

public class MerkleTreeNode
{
    public byte[] Hash { get; set; }

    public MerkleTreeNode(byte[] hash);

    public int Compare(MerkleTreeNode other);
    public int Compare(byte[] hashOther);
    public bool Matches(byte[] hashOther);
    public bool Matches(MerkleTreeNode other);
    public MerkleTreeNode Clone();
}
```

`MerkleTreeNodeComparer` is the `IComparer<MerkleTreeNode>` over those hash bytes, reachable as `MerkleTreeNodeComparer.Current`.

`MerkleProof` is default-constructible (both lists start empty), but `MerkleTreeNode` and `LeanIMTNodeEntry` are **not** — each declares only the constructor shown, so `new MerkleTreeNode { Hash = h }` does not compile; write `new MerkleTreeNode(h)` and `new LeanIMTNodeEntry(level, index, value)`.

## Notes and gotchas

- **`MerkleProof` is not what `MerkleTree<T>` returns.** `MerkleTree<T>.GetProof` returns a bare `List<byte[]>` (sorted pairing needs no direction); `LeanIncrementalMerkleTree<T>.GenerateProof` and `FrontierMerkleTree.VerifyProof` use the `MerkleProof` type with its `PathIndices`.
- **Pairing must match the verifier.** Sorted pairing is OpenZeppelin's convention; a contract written against a normal-order tree will reject sorted proofs and vice versa.
- **A `[Struct]` type must mirror the Solidity struct**: same parameter types, same order, correct `[Parameter]` order indices. A mismatch produces a different leaf hash and a silently unverifiable proof.
- **`FrontierMerkleTree.Append` takes a hash, not an item** — hash the leaf yourself before appending.
- **`IByteArrayConvertor<T>` has two methods.** A convertor that only implements `ConvertToByteArray` will not compile.
- **Duplicate leaves** are permitted, but `GetProof(T item)` and `LeanIncrementalMerkleTree<T>.IndexOf` both resolve to the *first* match.
- **`GetProof` throws when the leaf is absent** — `MerkleTree<T>.GetProof(byte[] hashLeaf)` raises `Exception("Leaf not found")` rather than returning an empty proof.
- **The `byte[] itemHash` overload of the instance `VerifyProof` always verifies with `Sorted` pairing**, whatever the tree was constructed with — it does not forward the tree's `PairingConcatType`. On a `Normal`-order tree, use the `T item` overload or the static one and pass the pairing explicitly (`MerkleTree.cs:119`).
- **`AbiStructSha3KeccackHashByteArrayConvertor<T>.ConvertFromByteArray` throws `NotSupportedException`** — hashing is one-way, so an OpenZeppelin tree can prove a leaf but cannot recover it.
- **`FrontierMerkleTree.Append` throws `InvalidOperationException` once `NextIndex` reaches `Capacity`** — the tree is fixed-depth and does not grow.

## Related packages

- **`Nethereum.Merkle.Patricia`** — the Ethereum state trie: Modified Merkle Patricia Trie, path-keyed node storage, EIP-1186 and snap/1 range proofs.
- **`Nethereum.Merkle.Binary`** — EIP-7864 binary Merkle trie for stateless execution.
- **`Nethereum.ABI`** — the `[Struct]` / `[Parameter]` attributes and the packed encoders the ABI-struct trees use.
- **`Nethereum.Util`** — `IHashProvider`, `IByteArrayConvertor<T>`, `Sha3Keccack`, `PoseidonHasher`.
