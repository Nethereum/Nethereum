# Nethereum.Merkle.Binary

The EIP-7864 binary Merkle trie — the proposed replacement for Ethereum's hexary Merkle Patricia Trie. A depth-256 binary tree over 32-byte keys, where an account's basic data, code hash and first 64 storage slots share one leaf node instead of a subtree.

## What you can do with it

- **Compute an EIP-7864 state root** over accounts, code chunks and storage slots.
- **Map an account onto the tree** — derive the keys for its balance and nonce, its code hash, each code chunk, and each storage slot.
- **Prove a key against a root**, and verify a proof you were given without holding the tree.
- **Produce and verify a block state diff** — the stems and sub-indices a block changed, plus the roots it moves between, which is the unit stateless verification needs.
- **Chain a run of blocks**, checking each block's post-root is the next one's pre-root.
- **Sync one contract, or the top of the tree only** — the node store indexes by depth and by address, so a checkpoint or a per-contract slice is a single call.
- **Swap the hash function** — Blake3 for the spec vectors, Poseidon for ZK, SHA-256 for anything else.

## Quick start

Put a value, take the root, prove the key, verify the proof.

*From `ProofTests.Proof_SingleEntry_Verifies` (use case `binary-trie-proofs`) — a tagged, passing test. `CreateTrie`, `MakeKey` and `MakeVal` are that test class's own private helpers, reproduced here from `tests/Nethereum.Merkle.Binary.Tests/ProofTests.cs:15-32` so the block runs as written.*

```csharp
static byte[] MakeKey(byte prefix, byte leafIdx)
{
    var k = new byte[32];
    k[0] = prefix;
    k[31] = leafIdx;
    return k;
}

static byte[] MakeVal(byte b)
{
    var v = new byte[32];
    v[0] = b;
    return v;
}

BinaryTrie CreateTrie(IHashProvider hp = null)
{
    return new BinaryTrie(hp ?? new Sha256HashProvider());
}

var trie = CreateTrie();
var key = MakeKey(0x00, 1);
var val = MakeVal(0xAA);
trie.Put(key, val);

var prover = new BinaryTrieProver(trie);
var proof = prover.BuildProof(key);
Assert.NotNull(proof);
Assert.NotEmpty(proof.Nodes);

var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
Assert.NotNull(result);
Assert.Equal(val, result);
```

Verification is by reconstruction, so a proof that does not rebuild the root returns `null` rather than throwing.

## Entry points

**Start with `BinaryTrie`.** `new BinaryTrie()` gives you a working tree — hashed with SHA-256, which is the default, *not* the Blake3 the EIP-7864 spec vectors use. Everything else hangs off it.

| I want to… | Reach for |
|---|---|
| **Build a tree and get its root** | **`new BinaryTrie()` → `Put(key, value)` → `ComputeRoot()`** |
| Use a specific hash | `new BinaryTrie(new Blake3HashProvider())` |
| Turn an account into tree keys | `new BinaryTreeKeyDerivation(hashProvider).GetTreeKeyForBasicData(address)` and friends |
| Pack an account header leaf | `BasicDataLeaf.Pack(version, codeSize, nonce, balance)` |
| Split code into chunks | `CodeChunker.ChunkifyCode(code)` |
| Prove a key | `new BinaryTrieProver(trie).BuildProof(key)` |
| Verify a proof | `new BinaryTrieProofVerifier(hashProvider).VerifyProof(root, key, proof)` |
| Persist the tree | `trie.SaveToStorage(new InMemoryBinaryTrieNodeStore())` |
| Produce a block diff | `BinaryTrieStateDiffProducer.Produce(blockNumber, preRoot, postRoot, nodeStore)` |
| Verify a block diff | `new BinaryTrieStateDiffVerifier(hashProvider).Verify(diff, preTrie)` |

## Installation

```bash
dotnet add package Nethereum.Merkle.Binary
```

## Shape of the trie

```csharp
public static class BinaryTrieConstants
{
    public const int StemNodeWidth = 256;
    public const int StemSize = 31;
    public const int HashSize = 32;
    public const int BitmapSize = 32;
    public const int NodeTypeBytes = 1;
    public const int ValueMerkleLevels = 8;

    public const byte NodeTypeStem = 1;
    public const byte NodeTypeInternal = 2;

    public static byte[] ZeroHash { get; }
    public static bool IsZeroHash(byte[] hash);
}
```

`StemSize` is the 31 key bytes that identify a stem and `StemNodeWidth` the 256 values held under it; `ValueMerkleLevels` is **8** — the depth of the binary merkleisation over those values, which is what makes 2⁸ = 256 = `StemNodeWidth` of them. `NodeTypeStem` / `NodeTypeInternal` and `BitmapSize` belong to the compact node encoding. `ZeroHash` and `IsZeroHash` express the EIP-7864 zero-propagation shortcut: `hash([0x00] * 64) = [0x00] * 32`, so an all-empty subtree collapses to zeros instead of hashing.

Node types implement one interface, `IBinaryNode` (`src/Nethereum.Merkle.Binary/Nodes/IBinaryNode.cs`):

```csharp
public interface IBinaryNode
{
    byte[] Get(byte[] key, NodeResolverFunc resolver);
    IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth);
    byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver);
    IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth);
    byte[] ComputeHash(IHashProvider hashProvider);
    IBinaryNode Copy();
    int GetHeight();
}
```

`Insert` and `InsertValuesAtStem` return the *replacement* node rather than mutating in place, which is how `Copy()` can hand out an independent pre-state trie for diff verification.

| Node | Role |
|---|---|
| `StemBinaryNode` | holds `Stem` and its `Values` (256 slots); `GetHeight()` is always 1 |
| `InternalBinaryNode` | `Left` / `Right` children, branching on one bit of the key |
| `HashedBinaryNode` | a `Hash` standing in for a subtree not in memory; resolved through the `NodeResolverFunc` |
| `EmptyBinaryNode` | the empty subtree; shared `EmptyBinaryNode.Instance` |

`NodeResolverFunc` is `delegate byte[] NodeResolverFunc(byte[] path, byte[] hash)` — the hook the trie calls to pull a missing node's blob from storage. It is supplied through `BinaryTrieOptions { HashProvider, NodeResolver }`. `BinaryTrieOptions.Default` leaves `NodeResolver` null — nothing is pulled from storage — and sets `HashProvider = new Sha256HashProvider()` (`BinaryTrieOptions.cs:12-15`), so **`new BinaryTrie()` hashes with SHA-256, not Blake3**. The EIP-7864 spec vectors are Blake3: a trie built with the default hash produces a different root, so pass `new BinaryTrie(new Blake3HashProvider())` when the root has to match the spec or a peer.

## Usage

Every snippet below is extracted verbatim from a `[NethereumDocExample(DocSection.ChainInfrastructure, …)]`-tagged passing test in `tests/Nethereum.Merkle.Binary.Tests`.

**They are fragments, not programs.** Extraction keeps the body of the test method, so a snippet may call the test class's private helpers (`MakeKey`, `MakeValue`, `MakeVal`, `CreateTrie`, `CreateBlake3Trie`, `BuildTrieWithTwoAccounts`, `ProduceOneDiff`) or its fields (`_keyDerivation`, `_hashProvider`), and it may end in `Assert`. Open the named test file for the setup before copying — only the Quick Start above is reproduced complete.

### Put and get

*From `BinaryTrieTests.SingleEntry_PutGet_ReturnsValue` (use case `binary-trie`).*

```csharp
var trie = new BinaryTrie();
var key = MakeKey(0x00, 0x01);
var value = MakeValue(0xAB);

trie.Put(key, value);
var result = trie.Get(key);

Assert.Equal(value, result);
```

`new BinaryTrie()` uses `BinaryTrieOptions.Default`, and therefore **SHA-256** — swap in `new BinaryTrie(new Blake3HashProvider())` for EIP-7864 spec roots. The other constructors are `BinaryTrie(BinaryTrieOptions options)`, `BinaryTrie(IHashProvider hashProvider)`, and the static `BinaryTrie.FromRootHash(byte[] rootHash, BinaryTrieOptions options)` for attaching to an existing root through a resolver.

### Round-trip with Blake3, and what deletion means

*From `BinaryTrieSpecVectorTests.PutGetDelete_RoundTrip_Blake3` (use case `binary-trie`).*

```csharp
var trie = CreateBlake3Trie();
var key = new byte[32]; key[31] = 0x01;
var val = "deadbeef00000000000000000000000000000000000000000000000000000000".HexToByteArray();

trie.Put(key, val);
var got = trie.Get(key);
Assert.Equal(val, got);

var hashAfterPut = trie.ComputeRoot();
Assert.NotEqual(new byte[32], hashAfterPut);

trie.Delete(key);
got = trie.Get(key);
Assert.Null(got);
```

`Delete` marks the sub-index absent. The companion test `Delete_ProducesAbsentRoot_NotZeroValueRoot_Blake3` pins the distinction that matters for consensus: an *absent* value and a *zero* value do not produce the same root.

### The rest of the trie surface

| Member | Purpose |
|---|---|
| `byte[] Get(byte[] key)` / `void Put(byte[] key, byte[] value)` / `void Delete(byte[] key)` | single-key access |
| `byte[][] GetValuesAtStem(byte[] stem)` / `void PutStem(byte[] stem, byte[][] values)` | read/write a whole stem's 256 slots at once |
| `void ApplyBatch(IEnumerable<KeyValuePair<byte[], byte[]>> entries)` | bulk insert |
| `byte[] ComputeRoot()` | the state root |
| `BinaryTrie Copy()` | independent copy (used to keep a pre-state trie for diff verification) |
| `int GetHeight()` | tree height |
| `List<IBinaryNode> FindPath(byte[] stem)` | root-to-stem node path |
| `IHashProvider HashProvider` | the hash in use |
| `void SaveToStorage(IBinaryTrieStorage storage)` | persist the trie's nodes |

## Key derivation — mapping accounts onto stems

`BinaryTreeKeyDerivation` turns an address plus a tree index into a 32-byte key.

| Member | Value / meaning |
|---|---|
| `BasicDataLeafKey` | `0` — sub-index of the packed basic-data leaf |
| `CodeHashLeafKey` | `1` — sub-index of the code hash |
| `HeaderStorageOffset` | `64` — where the account header's inline storage slots begin |
| `CodeOffset` | `128` — where code chunks begin |
| `byte[] GetTreeKey(byte[] address32, EvmUInt256 treeIndex, byte subIndex)` | the general form |
| `byte[] GetTreeKeyForBasicData(byte[] address)` | version, code size, nonce, balance |
| `byte[] GetTreeKeyForCodeHash(byte[] address)` | code hash |
| `byte[] GetTreeKeyForCodeChunk(byte[] address, ulong chunkId)` | one 31-byte code chunk |
| `byte[] GetTreeKeyForStorageSlot(byte[] address, EvmUInt256 storageKey)` | storage slot |
| `static byte[] AddressTo32(byte[] address)` | left-pads a 20-byte address to 32 |

```csharp
public class BinaryTreeKeyDerivation
{
    public const byte BasicDataLeafKey = 0;
    public const byte CodeHashLeafKey = 1;
    public const int HeaderStorageOffset = 64;
    public const int CodeOffset = 128;

    public byte[] GetTreeKey(byte[] address32, EvmUInt256 treeIndex, byte subIndex);
    public byte[] GetTreeKeyForBasicData(byte[] address);
    public byte[] GetTreeKeyForCodeHash(byte[] address);
    public byte[] GetTreeKeyForCodeChunk(byte[] address, ulong chunkId);
    public byte[] GetTreeKeyForStorageSlot(byte[] address, EvmUInt256 storageKey);

    public static byte[] AddressTo32(byte[] address);
}
```

The design point is *locality*: slots 0–63 land inside the account's header stem (`HeaderStorageOffset` … `CodeOffset - 1`), so a contract's hot slots are read with its balance and nonce in one node. Slot 64 onward moves to a different stem.

*From `KeyDerivationTests.GetTreeKeyForStorageSlot_MainStorage64_DifferentStem` (use case `binary-trie-keys`).*

```csharp
var addr = new byte[20]; addr[0] = 0x11;
var key63 = _keyDerivation.GetTreeKeyForStorageSlot(addr, (EvmUInt256)63);
var key64 = _keyDerivation.GetTreeKeyForStorageSlot(addr, (EvmUInt256)64);

bool stemsDiffer = false;
for (int i = 0; i < 31; i++)
    if (key63[i] != key64[i]) { stemsDiffer = true; break; }
Assert.True(stemsDiffer);
```

Two helpers complete the account mapping:

**`BasicDataLeaf`** packs version, code size, nonce and balance into one 32-byte leaf at fixed offsets:

```csharp
public static class BasicDataLeaf
{
    public const int VersionOffset = 0;
    public const int CodeSizeOffset = 5;
    public const int NonceOffset = 8;
    public const int BalanceOffset = 16;

    public static byte[] Pack(byte version, uint codeSize, ulong nonce, EvmUInt256 balance);
    public static void Unpack(byte[] leaf, out byte version, out uint codeSize, out ulong nonce, out EvmUInt256 balance);
}
```

**`CodeChunker`** cuts contract code into 31-byte pieces (zero-padding the tail) and stores each as a 32-byte chunk: byte 0 is the number of leading bytes that are PUSH data spilling in from the previous chunk, bytes 1–31 are the code. That header is what lets a chunk be executed or verified without its predecessor (`CodeChunkerTests.Push32_SpansBoundary`).

```csharp
public static byte[][] ChunkifyCode(byte[] code);
```

## Hashing

| Type | Purpose |
|---|---|
| `Blake3HashProvider` | Blake3 as an `IHashProvider`; `Blake3HashProvider(IBlake3Strategy strategy)` lets you swap in a native implementation for the default `ManagedBlake3Strategy` |
| `ValuesMerkleizer.Merkleize(byte[][] values, IHashProvider hashProvider)` | merkleises a stem's 256 values |
| `CachedValuesMerkleizer` | the same, incrementally — `MarkDirty(int subIndex)`, `MarkFullDirty()`, `ComputeRoot(byte[][] values, IHashProvider hashProvider)` recompute only the touched path |

The trie is hash-agnostic, and **the default is SHA-256** (`BinaryTrieOptions.Default`). `Blake3HashProvider` is what the EIP-7864 spec vectors require; the test suite also exercises Poseidon (`Verify_Poseidon_Passes`) and SHA-256. The hash is part of the root — two tries over the same keys under different providers do not agree.

## Proofs

`BinaryTrieProver` walks the trie and collects the nodes on a key's path; `BinaryTrieProofVerifier` replays them against a root and returns the value, or `null` if the proof does not reconstruct that root.

| Type | Surface |
|---|---|
| `BinaryTrieProof` | `byte[][] Nodes` |
| `BinaryTrieProver` | `BinaryTrieProver(BinaryTrie trie)`, `BinaryTrieProof BuildProof(byte[] key)` |
| `BinaryTrieProofVerifier` | `BinaryTrieProofVerifier(IHashProvider hashProvider)`, `byte[] VerifyProof(byte[] rootHash, byte[] key, BinaryTrieProof proof)` |

```csharp
public class BinaryTrieProof
{
    public byte[][] Nodes { get; set; }
}
```

*From `ProofTests.Proof_SingleEntry_Verifies` (use case `binary-trie-proofs`).*

```csharp
var trie = CreateTrie();
var key = MakeKey(0x00, 1);
var val = MakeVal(0xAA);
trie.Put(key, val);

var prover = new BinaryTrieProver(trie);
var proof = prover.BuildProof(key);
Assert.NotNull(proof);
Assert.NotEmpty(proof.Nodes);

var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
Assert.NotNull(result);
Assert.Equal(val, result);
```

Verification is by reconstruction, so failure is expressed as `null` rather than a thrown exception. Flipping one bit in one proof node is enough:

*From `ProofTests.Proof_TamperedNode_FailsVerification` (use case `binary-trie-proofs`).*

```csharp
proof.Nodes[0][1] ^= 0x01;
var tampered = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
Assert.Null(tampered);
```

`BuildProof` always returns a `BinaryTrieProof`; when the key is not in the trie, or the trie is empty, the proof simply does not reconstruct the root and the verifier returns `null` (`Proof_MissingKey_ReturnsNull`, `Proof_EmptyTrie_ReturnsNull`). It throws `ArgumentException` if the key is not exactly 32 bytes. The verifier also returns `null` for a null or empty proof and for a null root (`Proof_NullInputs_ReturnsNull`).

## State diffs — carrying one root to the next

A `BinaryTrieStateDiff` is the stateless-verification unit: the stems and sub-indices a block changed, plus the roots it moves between.

```csharp
public class BinaryTrieStateDiff
{
    public const byte VERSION = 1;

    public byte Version { get; set; }
    public long BlockNumber { get; set; }
    public byte[] PreStateRoot { get; set; }
    public byte[] PostStateRoot { get; set; }
    public List<StemDiff> StemDiffs { get; set; }
    public List<byte[]> ProofSiblings { get; set; }
}

public class StemDiff
{
    public byte[] Stem { get; set; }
    public List<SuffixDiff> SuffixDiffs { get; set; }
}

public class SuffixDiff
{
    public byte SuffixIndex { get; set; }
    public byte[] OldValue { get; set; }
    public byte[] NewValue { get; set; }
}

public class StateDiffVerificationResult
{
    public bool Success { get; }
    public string ErrorMessage { get; }
    public byte[] ComputedRoot { get; }
    public byte[] ExpectedRoot { get; }
    public int StemsApplied { get; }
    public int SuffixesApplied { get; }

    public static StateDiffVerificationResult Pass(int stems, int suffixes, byte[] root);
    public static StateDiffVerificationResult Fail(string message, byte[] computed, byte[] expected);
}
```

The diff is produced from the store's dirty nodes and verified back against the pre-state trie:

```csharp
public static BinaryTrieStateDiff Produce(
    long blockNumber,
    byte[] preStateRoot,
    byte[] postStateRoot,
    IBinaryTrieNodeStore nodeStore);
```

`BinaryTrieStateDiffEncoder.Encode(BinaryTrieStateDiff diff)` / `Decode(byte[] data)` are the wire format, and `BinaryTrieStateDiffVerifier(IHashProvider hashProvider)` exposes `Verify(BinaryTrieStateDiff diff, BinaryTrie preTrie)` plus `VerifySequence(...)` for a run of blocks.

*From `StateDiffVerifierTests.Verify_ValidDiff_Passes` (use case `binary-trie-state-diff`).*

```csharp
var (preTrie, diff) = ProduceOneDiff();
var verifier = new BinaryTrieStateDiffVerifier(_hashProvider);
var result = verifier.Verify(diff, preTrie);

Assert.True(result.Success, result.ErrorMessage);
Assert.True(result.StemsApplied > 0);
Assert.True(result.SuffixesApplied > 0);
```

`StateDiffVerificationResult` is constructed only through its two public factories, `Pass(stems, suffixes, root)` and `Fail(message, computed, expected)` — the properties are read-only, so a custom verifier reuses the same result type rather than setting fields.

The verifier applies the diff to the pre-state trie and checks the result against `PostStateRoot`. Tampering with either root, or with a value inside the diff, fails it — asserted by `Verify_TamperedPostRoot_Fails`, `Verify_TamperedPreRoot_Fails` and `Verify_TamperedValue_Fails` in the same file. `VerifySequence` chains blocks so each block's post-root must be the next block's pre-root.

## Storage

```csharp
public interface IBinaryTrieStorage
{
    void Put(byte[] key, byte[] value);
    byte[] Get(byte[] key);
    void Delete(byte[] key);
}

public interface IBinaryTrieNodeStore : IBinaryTrieStorage
{
    void PutNode(byte[] hash, byte[] encoded, int depth, byte nodeType, byte[] stem);

    void RegisterAddressStem(byte[] address, byte[] stemNodeHash);

    IReadOnlyList<NodeEntry> GetNodesByDepthRange(int minDepth, int maxDepth);

    IReadOnlyList<NodeEntry> GetStemNodesByAddress(byte[] address);

    IReadOnlyList<NodeEntry> GetDirtyNodes();

    void MarkBlockCommitted(long blockNumber);

    void ClearDirtyTracking();

    byte[] ExportCheckpoint(int maxDepth);

    void ImportCheckpoint(byte[] checkpoint);

    int NodeCount { get; }
}

public class NodeEntry
{
    public byte[] Hash { get; set; }
    public byte[] Encoded { get; set; }
    public int Depth { get; set; }
    public byte NodeType { get; set; }
    public byte[] Stem { get; set; }
    public long BlockNumber { get; set; }
    public bool IsDirty { get; set; }
}
```

`InMemoryBinaryTrieStorage` is the plain in-memory blob store (with a `Count`); `InMemoryBinaryTrieNodeStore` implements the full node-store contract. `CompactBinaryNodeCodec.Encode(IBinaryNode node, IHashProvider hashProvider)` / `Decode(byte[] data, int depth)` is the per-node format.

`BinaryTrieCheckpointSerializer` is the checkpoint format. It is **not** symmetric: it serialises `NodeEntry` but deserialises into `CheckpointEntry`, a separate public struct carrying only the five fields the checkpoint stores — no `BlockNumber`, no `IsDirty` (`Storage/BinaryTrieCheckpointSerializer.cs:6-13,54`):

```csharp
public struct CheckpointEntry
{
    public byte[] Hash;
    public byte[] Encoded;
    public int Depth;
    public byte NodeType;
    public byte[] Stem;
}

public static class BinaryTrieCheckpointSerializer
{
    public static byte[] Export(IReadOnlyList<NodeEntry> nodes);
    public static List<CheckpointEntry> Import(byte[] checkpoint);
}
```

The node store's depth and address indexes are what make partial sync possible: `GetNodesByDepthRange(0, maxDepth)` is a small top-of-tree checkpoint, and `GetStemNodesByAddress` pulls just one contract's stems (see `NodeStoreTests.PerContractSync_SimulateUsdcLightClient`).

*From `NodeStoreTests.ExportImportCheckpoint_RoundTrips` (use case `binary-trie-storage`).*

```csharp
var store = new InMemoryBinaryTrieNodeStore();
var trie = BuildTrieWithTwoAccounts();
trie.SaveToStorage(store);

var maxDepth = 5;
var checkpoint = store.ExportCheckpoint(maxDepth);
Assert.True(checkpoint.Length > 0);

var imported = new InMemoryBinaryTrieNodeStore();
imported.ImportCheckpoint(checkpoint);

var originalNodes = store.GetNodesByDepthRange(0, maxDepth);
var importedNodes = imported.GetNodesByDepthRange(0, maxDepth);

Assert.Equal(originalNodes.Count, importedNodes.Count);
```

## Utilities

`BinaryTrieUtils` exposes the bit helpers the trie navigates with: `GetBit(byte[] data, int bitIndex)`, `ByteArrayEquals(byte[] a, byte[] b)` and `ByteArrayEquals(byte[] a, byte[] b, int length)`.

## Related packages

- **`Nethereum.Merkle.Patricia`** — the hexary Merkle Patricia Trie this structure is proposed to replace, with path-keyed node storage and snap/1 range proofs.
- **`Nethereum.EVM.Core`** — `BlockFeatureConfig.BinaryBlake3(...)` / `BinaryPoseidon(...)` select the binary state tree and its hash in a block witness (`WitnessStateTreeType`, `WitnessHashFunction`).
