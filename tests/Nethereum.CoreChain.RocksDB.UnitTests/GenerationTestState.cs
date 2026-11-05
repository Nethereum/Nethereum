using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    internal sealed class GenerationTestState
    {
        public SortedDictionary<byte[], Account> Accounts { get; } = new(ByteArrayComparer.Current);
        public Dictionary<byte[], SortedDictionary<byte[], byte[]>> Storage { get; } = new(ByteArrayComparer.Current);
        public byte[] Root { get; private set; }

        public static byte[] HashOf(string domain, int index)
            => Sha3Keccack.Current.CalculateHash(System.Text.Encoding.ASCII.GetBytes($"{domain}:{index}"));

        public static byte[] SlotValue(int index) => ((BigInteger)(index + 1)).ToByteArray(isUnsigned: true, isBigEndian: true);

        public static Account NewAccount(ulong nonce, ulong balance, byte[] storageRoot = null, byte[] codeHash = null) => new Account
        {
            Nonce = nonce,
            Balance = balance,
            StateRoot = storageRoot ?? DefaultValues.EMPTY_TRIE_HASH,
            CodeHash = codeHash ?? DefaultValues.EMPTY_DATA_HASH,
        };

        public GenerationTestState WithEoas(string domain, int count)
        {
            for (int i = 0; i < count; i++) Accounts[HashOf(domain, i)] = NewAccount((ulong)i, 1);
            return this;
        }

        public GenerationTestState WithContract(byte[] owner, IEnumerable<(byte[] Slot, byte[] Value)> slots, byte[] codeHash = null)
        {
            var storage = new SortedDictionary<byte[], byte[]>(ByteArrayComparer.Current);
            foreach (var (slot, value) in slots) storage[slot] = value;
            Storage[owner] = storage;
            Accounts[owner] = NewAccount(1, 10, StorageRootOf(storage), codeHash);
            return this;
        }

        public GenerationTestState WithContract(string domain, int slots, byte[] codeHash = null)
            => WithContract(HashOf(domain, 0), Enumerable.Range(0, slots).Select(i => (HashOf(domain + "-slot", i), SlotValue(i))), codeHash);

        public GenerationTestState Clone()
        {
            var copy = new GenerationTestState();
            foreach (var account in Accounts) copy.Accounts[account.Key] = account.Value;
            foreach (var (owner, storage) in Storage) copy.Storage[owner] = new SortedDictionary<byte[], byte[]>(storage, ByteArrayComparer.Current);
            copy.Commit();
            return copy;
        }

        public GenerationTestState WithSlot(byte[] owner, byte[] slot, byte[] value)
        {
            if (value == null) Storage[owner].Remove(slot);
            else Storage[owner][slot] = value;
            var account = Accounts[owner];
            Accounts[owner] = new Account
            {
                Nonce = account.Nonce, Balance = account.Balance, CodeHash = account.CodeHash, StateRoot = StorageRootOf(Storage[owner]),
            };
            Commit();
            return this;
        }

        public GenerationTestState WithAccount(byte[] key, Account account)
        {
            Accounts[key] = account;
            Commit();
            return this;
        }

        public GenerationTestState WithoutAccount(byte[] key)
        {
            Accounts.Remove(key);
            Storage.Remove(key);
            Commit();
            return this;
        }

        public static byte[] StorageRootOf(SortedDictionary<byte[], byte[]> storage)
        {
            if (storage.Count == 0) return DefaultValues.EMPTY_TRIE_HASH;
            var trie = new StackTrie();
            foreach (var slot in storage) trie.Update(slot.Key, Nethereum.RLP.RLP.EncodeElement(slot.Value));
            return trie.Hash();
        }

        public byte[] Commit()
        {
            var trie = new StackTrie();
            foreach (var account in Accounts) trie.Update(account.Key, AccountEncoder.Current.Encode(account.Value));
            Root = Accounts.Count == 0 ? DefaultValues.EMPTY_TRIE_HASH : trie.Hash();
            return Root;
        }

        public void WriteTrie(RocksDbChainStoreBundle bundle)
        {
            Commit();
            using var writer = new BatchWriter(bundle.Rocks);
            foreach (var (owner, storage) in Storage)
            {
                if (!Accounts.ContainsKey(owner)) continue;
                var trie = new StackTrie(null, (path, blob) => writer.Put(RocksDbManager.CF_STATE_TRIE_STORAGE, owner.Concat(path).ToArray(), blob));
                foreach (var slot in storage) trie.Update(slot.Key, Nethereum.RLP.RLP.EncodeElement(slot.Value));
                if (storage.Count > 0) trie.Hash();
            }
            var accounts = new StackTrie(null, (path, blob) => writer.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path, blob));
            foreach (var account in Accounts) accounts.Update(account.Key, AccountEncoder.Current.Encode(account.Value));
            if (Accounts.Count > 0) accounts.Hash();
        }

        public void WriteFlat(RocksDbChainStoreBundle bundle)
        {
            var flat = new RocksDbStateStore(bundle.Rocks);
            foreach (var account in Accounts) flat.SaveAccountByHashAsync(account.Key, account.Value).GetAwaiter().GetResult();
            using var writer = new BatchWriter(bundle.Rocks);
            foreach (var (owner, storage) in Storage)
                foreach (var slot in storage) writer.Put(RocksDbManager.CF_STATE_STORAGE, owner.Concat(slot.Key).ToArray(), slot.Value);
        }

        public void Write(RocksDbChainStoreBundle bundle)
        {
            WriteTrie(bundle);
            WriteFlat(bundle);
        }

        public static List<(byte[] Key, byte[] Value)> Rows(RocksDbChainStoreBundle bundle, string column, byte[] prefix = null)
        {
            var rows = new List<(byte[], byte[])>();
            using var it = bundle.Rocks.CreateIterator(column);
            for (it.Seek(prefix ?? Array.Empty<byte>()); it.Valid(); it.Next())
            {
                if (prefix != null && !it.Key().Take(prefix.Length).SequenceEqual(prefix)) break;
                rows.Add((it.Key(), it.Value()));
            }
            return rows;
        }

        public static bool SameRows(List<(byte[] Key, byte[] Value)> a, List<(byte[] Key, byte[] Value)> b)
            => a.Count == b.Count && a.Zip(b).All(p => p.First.Key.SequenceEqual(p.Second.Key) && p.First.Value.SequenceEqual(p.Second.Value));

        public static Task<FlatStateReconcileResult> OracleVerify(RocksDbChainStoreBundle bundle, byte[] root)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), new HiddenPathStore(bundle.StateTrieNodes))
                .VerifyFlatStateAsync(root, null, CancellationToken.None);

        public static int UnitsDoneAtStart(IEnumerable<string> lines, int run = 0)
        {
            var start = lines.Where(l => l.StartsWith("snap.flat.certify start ", StringComparison.Ordinal)).ElementAt(run);
            var token = start.Split(' ').Single(t => t.StartsWith("units_done=", StringComparison.Ordinal));
            return int.Parse(token.Substring("units_done=".Length).Split('/')[0]);
        }

        public static int UnitsQueued(IEnumerable<string> lines, int run = 0)
        {
            var queue = lines.Where(l => l.StartsWith("certify.queue units=", StringComparison.Ordinal)).ElementAt(run);
            return int.Parse(queue.Split(' ')[1].Substring("units=".Length));
        }

        internal sealed class HiddenPathStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public HiddenPathStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private sealed class BatchWriter : IDisposable
        {
            private readonly RocksDbManager _rocks;
            private RocksDbSharp.WriteBatch _batch;
            private int _pending;

            public BatchWriter(RocksDbManager rocks)
            {
                _rocks = rocks;
                _batch = rocks.CreateWriteBatch();
            }

            public void Put(string column, byte[] key, byte[] value)
            {
                _batch.Put(key, value, _rocks.GetColumnFamily(column));
                if (++_pending < 20_000) return;
                _rocks.Write(_batch);
                _batch.Dispose();
                _batch = _rocks.CreateWriteBatch();
                _pending = 0;
            }

            public void Dispose()
            {
                _rocks.Write(_batch);
                _batch.Dispose();
            }
        }
    }

    internal sealed class ServingScheduler : IFetchRequestScheduler
    {
        private readonly RocksDbChainStoreBundle[] _servers;
        private long _nodesServed;

        public ServingScheduler(params RocksDbChainStoreBundle[] servers) => _servers = servers;

        public ConcurrentQueue<byte[]> TrieNodeRoots { get; } = new();
        public long NodesServed => Interlocked.Read(ref _nodesServed);

        public async Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
        {
            TrieNodeRoots.Enqueue(stateRoot);
            var server = _servers.FirstOrDefault(s => s.StateTrieNodes.ContainsKey(stateRoot)) ?? _servers[0];
            var reply = await new PatriciaSnapRequestHandler(server.StateTrieNodes, new NoBytecodes()).GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 1,
                RootHash = stateRoot,
                Paths = paths,
                ResponseBytes = responseBytes,
            }, ct);
            Interlocked.Add(ref _nodesServed, reply.Nodes.Count(n => n is { Length: > 0 }));
            return reply;
        }

        public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => throw new NotSupportedException();
        public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => throw new NotSupportedException();

        private sealed class NoBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }
    }

    public abstract class GenerationTestBase : IDisposable
    {
        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        protected sealed class SteppingClock : TimeProvider
        {
            private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

            public override DateTimeOffset GetUtcNow()
                => new DateTimeOffset(Interlocked.Add(ref _ticks, TimeSpan.FromSeconds(61).Ticks), TimeSpan.Zero);
        }

        protected static FlatStateReconciler Reconciler(RocksDbChainStoreBundle bundle, TimeProvider clock = null)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), bundle.StateTrieNodes, clock);

        protected RocksDbChainStoreBundle OpenPathKeyedBundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"generation_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            return bundle;
        }
    }
}
