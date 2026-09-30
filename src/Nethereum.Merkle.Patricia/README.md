# Nethereum.Merkle.Patricia

The Modified Merkle Patricia Trie — the data structure behind Ethereum account state, contract storage, and transaction and receipt roots. Build one, commit it, prove a key against its root, or verify someone else's proof without holding the state.

## What you can do with it

- **Compute a state, storage or transaction root** from a set of key/value pairs.
- **Prove a key** — generate the root-first, EIP-1186 node list a light client or contract needs.
- **Verify an account, a storage slot or a transaction** against a trusted root, without the rest of the state.
- **Serve and verify snap/1 range proofs** — hand out a slice of the trie with edge proofs, and check one you were given.
- **Store trie nodes by path instead of by hash**, so an updated node overwrites its predecessor and the database does not grow with history.
- **Keep many contract storage tries in one store**, separated by owner, without slot-key collisions.
- **Build a trie larger than memory**, persisting and collapsing as you go.

## Quick start

Compute a root, prove a key against it, and verify the proof.

*From `ProofVerificationFrontDoorTests.QuickStart_ComputeARootProveAKeyAndVerifyIt` (use case `quick-start`) — a tagged, passing test.*

```csharp
var hashProvider = Sha3KeccackHashProvider.Instance;
var trie = new PatriciaTrie(new InMemoryContentNodeStore());

for (var i = 0; i < 128; i++)
    trie.Put(hashProvider.ComputeHash(new[] { (byte)i }), new byte[] { (byte)i, 0xAB });
trie.SaveNodesToStorage();

var root = trie.Root.GetHash();
var key = hashProvider.ComputeHash(new byte[] { 7 });
var proof = ProofGenerator.GenerateProof(trie, key);

Assert.Equal(root, hashProvider.ComputeHash(proof[0]));
Assert.True(ProofVerification.Current.Range.VerifyEntry(root, key, new byte[] { 7, 0xAB }, proof));
```

## Entry points

**Start with `PatriciaTrie`.** It holds its own node store, so `Get`, `Put` and `Delete` take no storage argument.

| I want to… | Reach for |
|---|---|
| **Build a trie and get its root** | **`new PatriciaTrie(store)` → `Put(key, value)` → `SaveNodesToStorage()` → `Root.GetHash()`** |
| Reload a trie from a root | `new PatriciaTrie(rootHash, store)` |
| Prove a key | `ProofGenerator.GenerateProof(trie, key)` |
| Verify an account or storage proof | `ProofVerification.Current.Account.Verify(…)` / `.Storage.Verify(…)` |
| Serve a snap/1 range | `PatriciaRangeIterator.EnumerateRange(…)` + `PatriciaRangeProofGenerator.GenerateProof(…)` |
| Verify a snap/1 range | `ProofVerification.Current.Range.Verify(…)` |
| Keep a contract's storage trie apart | `new PatriciaTrie(store, owner: keccak(address))` |
| Build something bigger than memory | `SaveDirtyNodesToStorageAndCollapse()` every N writes |

Two node stores ship in the box: `InMemoryContentNodeStore` (keyed by hash — what proof verification uses) and `InMemoryPathNodeStore` (keyed by owner and path — what a node uses). Persistent RocksDB implementations live in `Nethereum.CoreChain.RocksDB`.

## Installation

```bash
dotnet add package Nethereum.Merkle.Patricia
```

## Architecture: the trie holds one node store

The trie **holds a single node store** (`ITrieNodeStore`) rather than threading a storage object through every `Get`/`Put`/`Delete`. Each node carries its own identity — its hash (`GetHash()`) and its location (`Owner`, `Path`) — and **the store alone decides which of the two it keys on**.

That one decision is the whole difference between the two storage models:

| | Content-addressed store | Path-keyed (location-addressed) store |
|---|---|---|
| Key | `node.GetHash()` | `Owner ‖ Path` |
| Nodes stored | every node, including embedded <32-byte ones | only nodes ≥32 bytes; shorter children are inlined in the parent and re-decoded on read |
| Effect of an update | a new key appears; the old node stays | the same key is **overwritten** — the trie does not grow with history |
| Deletions | nothing to do | need tombstones, emitted from an `ITrieTracer` |
| Integrity on read | implicit (the key *is* the hash) | explicit — the store verifies the blob against the reference hash it was asked for |
| In-box implementation | `InMemoryContentNodeStore` | `InMemoryPathNodeStore` |

`Owner` is empty for the account trie and `keccak(address)` for a contract's storage trie, so many storage tries coexist in one path store without colliding on slot keys. This single-store model is what lets the same trie code run over an in-memory content store (proofs) or a persistent path-keyed database (a full node's state) with no API change.

Persistent RocksDB implementations of `ITrieNodeStore` — path-keyed and content-addressed, with node history — live in **`Nethereum.CoreChain.RocksDB`** (`src/Nethereum.CoreChain.RocksDB/Stores/`).

## Key concepts

- **Nodes** — `LeafNode` (path + value), `ExtendedNode` (shared-prefix path + one child), `BranchNode` (16 children + optional value), `HashNode` (a 32-byte reference that lazily decodes its inner node through the held store), `EmptyNode`.
- **Nibbles** — keys are traversed a nibble (4 bits) at a time; leaves and extensions carry hex-prefix-encoded nibble paths.
- **Node identity** — every `Node` has `GetHash()` (keccak of its RLP), plus `Owner` and `Path`. The store keys on one or the other.
- **`TrieNodeSet`** — the unit of a write: a batch of nodes to persist, plus `TrieNodeDelete` tombstones for removals. The trie collects dirty nodes into a set and calls `store.Commit(set)`.
- **Hash provider** — every constructor has an overload taking an `IHashProvider`; the default is `Sha3KeccackHashProvider.Instance` (`Nethereum.Util.HashProviders`).

## The node store

| Type | Role |
|---|---|
| `ITrieNodeStore` | The unified store the trie holds: `Commit(TrieNodeSet nodes)`, `Get(Node reference)` (returns raw RLP; the store never decodes), `Contains(Node reference)`, `ContainsKey(byte[] stateRoot)`, `Flush()`, `Clear()`. |
| `InMemoryContentNodeStore` | Content-addressed in-memory store (keyed by keccak). Also implements `INodeBlobStore`. Stores all nodes; self-verifying. The accumulator every proof verifier uses. |
| `InMemoryPathNodeStore` | Location-addressed in-memory store (keyed by `owner ‖ path`). Skips <32-byte nodes, verifies on read, applies tombstones. |
| `ContentAddressedNodeStore` | Adapter presenting a raw `INodeBlobStore` as a content-addressed `ITrieNodeStore` (`ContentAddressedNodeStore.Wrap(INodeBlobStore storage)`). |
| `INodeBlobStore` | The raw hash→blob byte store: `Put(byte[] key, byte[] value)`, `Get(byte[] key)`, `Delete(byte[] key)`. |
| `IRawNodeReader` | `TryGetRawNode(byte[] owner, byte[] path)` — reattach a trie to a root read straight out of a path store (`PatriciaTrie.ReattachFromRawRoot`). |
| `ITrieTracer` / `TrieTracer` | Observes node removals (`OnRemove(byte[] owner, byte[] path, byte[] prevBlob)`, `Removals`, `Reset()`) so a path-store commit can emit tombstones. Zero cost when absent (content mode). |
| `IContractStorageWipeable` | `DeleteRange(byte[] owner)` — wipes a whole contract's storage subtree (SELFDESTRUCT) on a path store. |
| `TransientFlushUnavailableException` | Raised by a store that cannot honour a `Flush()` at this moment. |

### Storage declarations

These declarations are copied from the source files under `src/Nethereum.Merkle.Patricia/Storage/`.

`ReadmeTraceabilityTests` (`tests/Nethereum.Merkle.Patricia.Tests/ReadmeTraceabilityTests.cs`) checks that each tagged type is *named* in a README code block, and that **every public member it declares — properties, fields and methods — is named somewhere in this file**. Interfaces are covered by that: rename or remove `TryGetRawNode`, `Put`, `DeleteRange` or any member below and the test goes red naming the member.

Argument **order** is checked too, but only where this README writes the member with parentheses, and the check is a subsequence scan over the whole block rather than a signature parse. So a swapped pair is caught when the block names those parameters once, and can be masked when a neighbouring signature in the same block supplies the same names in the old order. Treat the suite as a reliable guard against renames and removals, and read the source file when the exact parameter order matters.

```csharp
public interface ITrieNodeStore
{
    void Commit(TrieNodeSet nodes);
    byte[] Get(Node reference);
    bool Contains(Node reference);
    void Flush();
    void Clear();

    bool ContainsKey(byte[] stateRoot);
}

public interface INodeBlobStore
{
    void Put(byte[] key, byte[] value);
    byte[] Get(byte[] key);
    void Delete(byte[] key);
}

public interface IRawNodeReader
{
    byte[] TryGetRawNode(byte[] owner, byte[] path);
}

public interface IContractStorageWipeable
{
    void DeleteRange(byte[] owner);
}
```

A commit carries removals as well as writes. `TrieNodeSet.AddDelete(byte[] owner, byte[] path, byte[] prevBlob)` records one:

```csharp
public readonly struct TrieNodeDelete
{
    public byte[] Owner { get; }
    public byte[] Path { get; }
    public byte[] PrevBlob { get; }

    public TrieNodeDelete(byte[] owner, byte[] path, byte[] prevBlob);
}
```

A content store ignores those — its keys are hashes, so a superseded node is simply never referenced again. A path store must apply them, because the key would otherwise still resolve.

## Usage

Every snippet below is extracted verbatim from a `[NethereumDocExample(DocSection.ChainInfrastructure, …)]`-tagged passing test in `tests/Nethereum.Merkle.Patricia.Tests`.

**They are fragments, not programs.** Extraction keeps the body of the test method, so a snippet may use variables built in the test's arrange step or private fixture helpers (`KeyHash`, `Value`, `RefOf`, `keys`, `values`, `rootHash`, …) that are not shown here, and it may end in `Assert`. Open the named test file for the surrounding setup before copying — only the Quick Start above is self-contained.

### Build, commit, reload — the store is held, not threaded

*From `ProofVerificationFrontDoorTests.BuildCommitAndReloadThroughOneHeldStore` (use case `patricia-trie`).*

```csharp
var keccak = new Sha3Keccack();
var store = new InMemoryContentNodeStore();
var trie = new PatriciaTrie(store);

var keys = new List<byte[]>();
var values = new List<byte[]>();
for (var i = 0; i < 200; i++)
{
    var key = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
    var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
    keys.Add(key);
    values.Add(value);
    trie.Put(key, value);
}
trie.SaveNodesToStorage();

var rootHash = trie.Root.GetHash();
var reloaded = new PatriciaTrie(rootHash, store);

for (var i = 0; i < keys.Count; i++)
    Assert.Equal(values[i], reloaded.Get(keys[i]));
```

`HeldStoreTrieTests.Reads_Through_Held_Store_Without_Threading` runs the same reload over **both** store kinds from one `[Theory]`, which is the point: the trie code does not know which it is holding.

`Get`/`Put`/`Delete` take **no** storage parameter. `PatriciaTrie.LoadFromStorage(rootHash, store)` is the `(rootHash, store)` constructor *plus an empty-root branch*: a null or empty `rootHash` returns `new PatriciaTrie(store, …)` — an empty trie — where the constructor would build a `HashNode` over those bytes (`PatriciaTrie.cs:120-125`). Use `LoadFromStorage` when the root may not exist yet.

`SaveNodesToStorage()` commits the whole trie; `SaveDirtyNodesToStorage()` commits only what changed.

### Account and contract-storage tries in one path store

*From `PathStoreAppliesTombstonesTests.Storage_Trie_Owner_Keyed_Deletes_Do_Not_Touch_Account_Trie` (use case `key-path-storage`).*

```csharp
var store = new InMemoryPathNodeStore();
var keccak = new Sha3Keccack();
var owner = keccak.CalculateHash(new byte[] { 0xC0, 0xDE });

var account = new PatriciaTrie(store) { Tracer = new TrieTracer() };
var storage = new PatriciaTrie(store, owner) { Tracer = new TrieTracer() };
foreach (var k in keys)
{
    account.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xAC }));
    storage.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x57 }));
}
account.SaveDirtyNodesToStorage();
storage.SaveDirtyNodesToStorage();
```

Both tries use the *same* keys and the *same* store. Deleting every storage slot afterwards leaves the account trie intact, because the path store keys each node on `owner ‖ path` and `owner` differs. Attaching a `TrieTracer` is what lets the commit emit tombstones for the removed nodes.

### A path store verifies what it hands back

*From `UnifiedITrieNodeStoreTests.Path_Store_Commit_Resolves_Referenceable_Nodes_And_Rejects_Tamper` (use case `key-path-storage`).*

```csharp
var pathStore = new InMemoryPathNodeStore();
((ITrieNodeStore)pathStore).Commit(set);

foreach (var node in referenceable)
    Assert.Equal(node.GetEncodedData(), ((ITrieNodeStore)pathStore).Get(RefOf(node)));

var victim = referenceable[0];
var wrongRef = new HashNode { Hash = new byte[32], Owner = victim.Owner, Path = victim.Path };
Assert.Throws<InvalidOperationException>(() => ((ITrieNodeStore)pathStore).Get(wrongRef));
```

A content store cannot be lied to — the key *is* the hash. A path store can, so it re-hashes the blob on every read and throws when the blob does not match the hash the caller asked for. The same check is exposed directly as `ProofVerification.Current.TrieNode.Verify(expectedHash, blob, hashProvider)`.

### Bounded memory on large builds — save and collapse

*From `PatriciaTrieSaveAndCollapseTests.RootHash_IsIdentical_WithPeriodicCollapse` (use case `patricia-trie`).*

```csharp
var store = new InMemoryContentNodeStore();
var trie = new PatriciaTrie(store);
for (int i = 0; i < 2000; i++)
{
    trie.Put(KeyHash(i), Value(i));
    if (i % 250 == 249) trie.SaveDirtyNodesToStorageAndCollapse();
}
var actual = trie.Root.GetHash();
```

`SaveDirtyNodesToStorageAndCollapse()` persists the dirty nodes and then drops the materialised subtrees back to `HashNode`s, so a multi-million-key build does not have to fit in memory. The root is byte-for-byte the same as building without collapsing.

## Proofs

### Generating an account/storage proof (root-first, EIP-1186 order)

*From `ProofGeneratorRootFirstOrderingTests.FirstProofNode_HashesTo_StateRoot` (use case `state-proofs`).*

```csharp
var proof = ProofGenerator.GenerateProof(trie, keys[0]);

Assert.NotNull(proof);
Assert.NotEmpty(proof);
Assert.Equal(rootHash, new Sha3Keccack().CalculateHash(proof[0]));
```

```csharp
public static List<byte[]> GenerateProof(PatriciaTrie trie, byte[] key)
```

It returns a `List<byte[]>` of RLP node blobs, root first. **It returns `null` when the key is not in the trie** — there is no path to prove, so a caller must null-check rather than expect an empty list.

### Verifying against a trusted root

All verifiers hang off the `ProofVerification.Current` front door — `Account`, `Storage`, `Range`, `Transaction`, `TrieNode`:

| Verifier | Signature |
|---|---|
| `IAccountProofVerifier` | `bool Verify(byte[] stateRoot, IEnumerable<byte[]> proof, string accountAddress, Account account)` |
| `IStorageProofVerifier` | `bool Verify(byte[] stateRoot, IList<byte[]> proof, byte[] key, byte[] value)` |
| `IRangeProofVerifier` | `RangeProofResult Verify(byte[] rootHash, byte[] firstKey, IList<byte[]> keys, IList<byte[]> values, IList<byte[]> proofNodes)` and `bool VerifyEntry(byte[] root, byte[] keyHash, byte[] expectedValue, IList<byte[]> proof)` |
| `ITransactionProofVerifier` | `bool Verify(string transactionsRoot, List<IndexedSignedTransaction> transactions)` |
| `ITrieNodeVerifier` | `bool Verify(byte[] expectedHash, byte[] blob, IHashProvider hashProvider)` |

Each concrete verifier also exposes its own `Current` singleton, and `ProofVerification` has a constructor taking all five so you can substitute one. The concrete class names do **not** follow one pattern — the range verifier is `PatriciaRangeProofVerifier`, not a `RangeProofVerification`:

| Front-door property | Concrete class with `Current` |
|---|---|
| `Account` | `AccountProofVerification` |
| `Storage` | `StorageProofVerification` |
| `Range` | `PatriciaRangeProofVerifier` |
| `Transaction` | `TransactionProofVerification` |
| `TrieNode` | `TrieNodeVerification` |

```csharp
public interface IProofVerification
{
    IAccountProofVerifier Account { get; }
    IStorageProofVerifier Storage { get; }
    IRangeProofVerifier Range { get; }
    ITransactionProofVerifier Transaction { get; }
    ITrieNodeVerifier TrieNode { get; }
}
```

*From `ProofVerificationFrontDoorTests.AccountProof_VerifiesAgainstTheStateRoot` (use case `state-proofs`).*

```csharp
var stateRoot = accountTrie.Root.GetHash();
var proof = ProofGenerator.GenerateProof(accountTrie, accountKey);

Assert.True(ProofVerification.Current.Account.Verify(stateRoot, proof, TargetAddress, account));
```

Swapping the balance for a value the state root does not commit to makes the same call return `false` (`AccountProof_WithATamperedBalance_IsRejected`).

Storage slots go through `AccountStorage.EncodeKeyForStorage` / `EncodeValueForStorage` (from `Nethereum.Model`) — the verifier applies that encoding itself, so you pass the *raw* slot and value:

*From `ProofVerificationFrontDoorTests.StorageProof_VerifiesASetSlot_AndAnAbsentKeyYieldsNoProof` (use case `state-proofs`).*

```csharp
var storageRoot = storageTrie.Root.GetHash();
var inclusion = ProofGenerator.GenerateProof(
    storageTrie, AccountStorage.EncodeKeyForStorage(slot, hashProvider));

Assert.True(ProofVerification.Current.Storage.Verify(storageRoot, inclusion, slot, slotValue));
```

Under the hood a verifier loads the proof nodes into an `InMemoryContentNodeStore`, builds a `PatriciaTrie(root, thatStore)`, and re-reads — a proof that reconstructs the authentic root is unforgeable.

### snap/1 range proofs

For snap sync, a range of the trie is served with edge proofs and verified without the full trie:

- `PatriciaRangeProofGenerator.GenerateProof(Node root, ITrieNodeStore store, byte[] startKey)` and the four-argument overload `(…, byte[] startKey, byte[] lastReturnedKey)` — boundary proof for a served range.
- `PatriciaRangeIterator.EnumerateRange(Node root, ITrieNodeStore store, byte[] startKey, int maxCount = int.MaxValue, long maxResponseBytes = long.MaxValue)` — lexicographic enumeration yielding `PatriciaRangeIterator.RangeEntry { KeyBytes, Value }` — a class **nested inside `PatriciaRangeIterator`**, not a top-level type — resolving `HashNode`s lazily through the store. `startKey` must be 32 bytes.
- `ProofVerification.Current.Range.Verify(…)` → `RangeProofResult { Valid, HasMore }` — the full snap/1 range-proof algorithm (proof-to-path, unset-internal, has-right-element).

```csharp
public static IEnumerable<RangeEntry> EnumerateRange(
    Node root,
    ITrieNodeStore store,
    byte[] startKey,
    int maxCount = int.MaxValue,
    long maxResponseBytes = long.MaxValue)

public class RangeEntry
{
    public byte[] KeyBytes { get; set; }
    public byte[] Value { get; set; }
}

public readonly struct RangeProofResult
{
    public bool Valid { get; }
    public bool HasMore { get; }

    public RangeProofResult(bool valid, bool hasMore);

    public static readonly RangeProofResult Invalid;
}
```

*From `PatriciaRangeProofVerifierTests.Verify_BoundedRange_RoundTrip_Succeeds_WithHasMore` (use case `snap-range-proofs`).*

```csharp
var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, startKey, lastKey);

var r = ProofVerification.Current.Range.Verify(rootHash, startKey, keys, values, proof);
Assert.True(r.Valid);
Assert.True(r.HasMore);
```

`HasMore` tells the syncing peer whether another chunk follows. Tampering with a value, dropping a key, or presenting keys out of order all make `Valid` false — the companion tests in the same file assert each of those.

## Node types (reference)

| Node | Members |
|---|---|
| `Node` (abstract base) | `Owner`, `Path`, `GetHash()`, `GetEncodedData()`, `MarkDirty()`, `IsDirty`, `NeedsPersist`, `ClearNeedsPersist()`, `MarkPersisted()` |
| `LeafNode` | `Nibbles`, `Value`, `GetPrefixedNibbles()` |
| `ExtendedNode` | `Nibbles`, `InnerNode`, `CollapseInner()`, `GetPrefixedNibbles()` |
| `BranchNode` | `Children` (16), `Value`, `SetChild(int nibble, Node node)`, `RemoveChild(int nibble)`, `CollapseChild(int nibble)` |
| `HashNode` | `Hash`, lazy `InnerNode`, `DecodeInnerNode(ITrieNodeStore store, bool decodeInnerHashNodes)`, `ReleaseInnerNode()` |
| `EmptyNode` | the empty subtree; shared `EmptyNode.Instance` |

Decoding is one unified path through `NodeDecoder`, which threads `Owner`/`Path` onto each child reference — no storage-type sniffing. Both members are **instance** methods, so you construct a `NodeDecoder` first (`Nodes/Rlp/NodeDecoder.cs:8,13`):

```csharp
public class NodeDecoder
{
    public Node Decode(HashNode reference, ITrieNodeStore store, bool decodeHashNodes);
    public Node DecodeFromRlpData(byte[] currentData, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store);
}
```

A 32-byte child becomes a `HashNode` carrying `{Hash, Owner, Path}`; a shorter child is decoded inline.

`PatriciaPathWalker` exposes the nibble/compact conversions the trie uses — `CompactToNibbles(byte[] compact)`, `NibblesToCompact(byte[] nibbles)`, `WalkPath(Node root, ITrieNodeStore store, byte[] pathNibbles)`.

## PatriciaTrie surface (reference)

| Member | Notes |
|---|---|
| Constructors | `()` (empty trie, Keccak), `(IHashProvider)`, `(byte[] hashRoot)`, `(Node root)`, `(ITrieNodeStore store)`, `(byte[] hashRoot, ITrieNodeStore store)`, `(Node root, ITrieNodeStore store)`, `(ITrieNodeStore store, byte[] owner)`, `(byte[] hashRoot, ITrieNodeStore store, byte[] owner)`, `(Node root, ITrieNodeStore store, byte[] owner)` — each with an extra `IHashProvider` overload |
| `LoadFromStorage` | `(byte[] rootHash, ITrieNodeStore store[, byte[] owner][, IHashProvider])` |
| `ReattachFromRawRoot` | `(IRawNodeReader rawReader, ITrieNodeStore store, byte[] owner[, IHashProvider])` |
| Properties | `Root`, `Store`, `HashProvider`, `Tracer` |
| Reads | `Get(byte[] key)` |
| Writes | `Put(byte[] key, byte[] value)`, `Delete(byte[] key)` |
| Commits | `SaveNodesToStorage()`, `SaveDirtyNodesToStorage()`, `SaveDirtyNodesToStorageAndCollapse()` |

## Related packages

- **`Nethereum.Merkle.Binary`** — the EIP-7864 binary trie, the proposed replacement for this structure.
- **`Nethereum.CoreChain.RocksDB`** — persistent path-keyed and content-addressed `ITrieNodeStore` implementations with node history and reorg rewind.
- **`Nethereum.Merkle`** — general-purpose Merkle trees (airdrops, whitelists, sparse trees), unrelated to the state trie.
