using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatStateTrieGenerator
    {
        internal const int DefaultStorageCollapseIntervalSlots = 5_000;
        internal const int DefaultAccountCollapseIntervalAccounts = 50_000;

        private const int ShardCount = 16;
        private const long ProgressAccountInterval = 1_000_000;

        private readonly RocksDbManager _rocks;
        private readonly RocksDbStateStore _flat;
        private readonly ITrieNodeStore _trieNodes;
        private readonly int _storageCollapseIntervalSlots;
        private readonly int _accountCollapseIntervalAccounts;

        public FlatStateTrieGenerator(
            RocksDbManager rocks, RocksDbStateStore flatStore, ITrieNodeStore trieNodes,
            int storageCollapseIntervalSlots = DefaultStorageCollapseIntervalSlots,
            int accountCollapseIntervalAccounts = DefaultAccountCollapseIntervalAccounts)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _flat = flatStore ?? throw new ArgumentNullException(nameof(flatStore));
            _trieNodes = trieNodes ?? throw new ArgumentNullException(nameof(trieNodes));
            if (storageCollapseIntervalSlots < 1) throw new ArgumentOutOfRangeException(nameof(storageCollapseIntervalSlots));
            if (accountCollapseIntervalAccounts < 1) throw new ArgumentOutOfRangeException(nameof(accountCollapseIntervalAccounts));
            _storageCollapseIntervalSlots = storageCollapseIntervalSlots;
            _accountCollapseIntervalAccounts = accountCollapseIntervalAccounts;
        }

        public Task<FlatTrieGenerationResult> GenerateAsync(byte[] expectedRoot, Action<string> progress, CancellationToken ct)
        {
            if (expectedRoot == null || expectedRoot.Length != 32)
                throw new ArgumentException("expectedRoot must be 32 bytes", nameof(expectedRoot));

            return Task.Run(async () =>
            {
                WipePathKeyedTrie();
                var accountTrie = new AccountTrieBuilder(new PatriciaTrie(_trieNodes), _accountCollapseIntervalAccounts);
                var shards = new Task<ShardCounters>[ShardCount];
                for (int i = 0; i < ShardCount; i++)
                {
                    int shard = i;
                    shards[shard] = Task.Run(() => GenerateShardAsync(shard, accountTrie, progress, ct), ct);
                }
                var counters = ShardCounters.Sum(await Task.WhenAll(shards).ConfigureAwait(false));

                var root = accountTrie.Commit();
                _trieNodes.Flush();
                var result = new FlatTrieGenerationResult(
                    root, counters.AccountsScanned, counters.SlotsScanned,
                    counters.AccountRootsRewritten, counters.DanglingSlotsDeleted);
                if (!ByteUtil.AreEqual(root, expectedRoot))
                    throw new InvalidOperationException(
                        $"snap.generate.root_mismatch generated=0x{root.ToHex()} expected=0x{expectedRoot.ToHex()} " +
                        $"accounts={result.AccountsScanned} slots={result.SlotsScanned} " +
                        $"account_roots_rewritten={result.AccountRootsRewritten} dangling_slots_deleted={result.DanglingSlotsDeleted}");
                return result;
            }, ct);
        }

        public void PruneBeyond(IReadOnlyList<SnapSyncAccountTask> durableTasks)
        {
            if (durableTasks == null) throw new ArgumentNullException(nameof(durableTasks));

            using var batch = _rocks.CreateWriteBatch();
            var accounts = _rocks.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            var storage = _rocks.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            foreach (var task in durableTasks)
            {
                DeleteRange(batch, accounts, task.Next, Concat(task.Last, Filled(0xff, 1)));
                PruneTaskStorage(batch, storage, task);
            }
            _rocks.Write(batch);
        }

        private static void PruneTaskStorage(WriteBatch batch, ColumnFamilyHandle storage, SnapSyncAccountTask task)
        {
            var from = Concat(task.Next, Filled(0x00, 32));
            foreach (var owner in ProtectedOwners(task))
            {
                DeleteRange(batch, storage, from, Concat(owner, Filled(0x00, 32)));
                from = Concat(owner, Filled(0xff, 33));
            }
            DeleteRange(batch, storage, from, Concat(task.Last, Filled(0xff, 33)));

            foreach (var owner in task.SubTasks)
                foreach (var subTask in owner.Value)
                    DeleteRange(batch, storage, Concat(owner.Key, subTask.Next), Concat(owner.Key, subTask.Last, Filled(0xff, 1)));
        }

        private static IEnumerable<byte[]> ProtectedOwners(SnapSyncAccountTask task)
            => task.StorageCompleted.Concat(task.SubTasks.Keys)
                .Where(owner => Compare(owner, task.Next) >= 0 && Compare(owner, task.Last) <= 0)
                .Distinct(ByteArrayComparer.Current)
                .OrderBy(owner => owner, ByteArrayComparer.Current);

        private static void DeleteRange(WriteBatch batch, ColumnFamilyHandle columnFamily, byte[] from, byte[] to)
        {
            if (Compare(from, to) >= 0) return;
            batch.DeleteRange(from, (ulong)from.Length, to, (ulong)to.Length, columnFamily);
        }

        private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

        private static byte[] Filled(byte value, int length) => Enumerable.Repeat(value, length).ToArray();

        private void WipePathKeyedTrie()
        {
            if (!_rocks.Options.PathKeyedState) return;
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            _rocks.WipeColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            (_trieNodes as ICacheInvalidatable)?.ClearCache();
        }

        private async Task<ShardCounters> GenerateShardAsync(
            int shard, AccountTrieBuilder accountTrie, Action<string> progress, CancellationToken ct)
        {
            var (shardStart, shardEnd) = ShardBounds(shard);
            var counters = new ShardCounters();
            var encoder = new AccountEncoder();
            using var accounts = _rocks.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS);
            using var storage = _rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            accounts.Seek(shardStart);
            storage.Seek(OwnerLowBound(shardStart));

            for (var accountHash = NextAccountKey(accounts, shardEnd); accountHash != null; accountHash = NextAccountKey(accounts, shardEnd))
            {
                ct.ThrowIfCancellationRequested();
                counters.DanglingSlotsDeleted += DeleteStorageBelow(storage, OwnerLowBound(accountHash), ct);

                var account = DecodeAccount(accountHash, accounts.Value());
                var storageRoot = BuildStorageTrie(accountHash, storage, counters, ct);
                if (!ByteUtil.AreEqual(storageRoot, account.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH))
                {
                    account.StateRoot = storageRoot;
                    await _flat.SaveAccountByHashAsync(accountHash, account).ConfigureAwait(false);
                    counters.AccountRootsRewritten++;
                }

                accountTrie.Put(accountHash, encoder.Encode(account));
                if (++counters.AccountsScanned % ProgressAccountInterval == 0)
                    progress?.Invoke($"snap.generate shard={shard:X} accounts={counters.AccountsScanned:N0} slots={counters.SlotsScanned:N0}");
                accounts.Next();
            }

            counters.DanglingSlotsDeleted += DeleteStorageBelow(storage, shardEnd == null ? null : OwnerLowBound(shardEnd), ct);
            return counters;
        }

        private byte[] BuildStorageTrie(byte[] accountHash, Iterator storage, ShardCounters counters, CancellationToken ct)
        {
            if (!IsStorageOf(storage, accountHash)) return DefaultValues.EMPTY_TRIE_HASH;

            var storageTrie = new PatriciaTrie(_trieNodes, accountHash);
            int sinceCollapse = 0;
            for (; IsStorageOf(storage, accountHash); storage.Next())
            {
                var slotHash = new byte[32];
                Buffer.BlockCopy(storage.Key(), 32, slotHash, 0, 32);
                storageTrie.Put(slotHash, AccountStorage.EncodeValueForStorage(storage.Value()));
                if (++sinceCollapse >= _storageCollapseIntervalSlots)
                {
                    storageTrie.SaveDirtyNodesToStorageAndCollapse();
                    sinceCollapse = 0;
                }
                if ((++counters.SlotsScanned & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            }
            storageTrie.SaveDirtyNodesToStorage();
            return storageTrie.Root.GetHash();
        }

        private long DeleteStorageBelow(Iterator storage, byte[] bound64, CancellationToken ct)
        {
            long deleted = 0;
            for (; storage.Valid() && (bound64 == null || Compare(storage.Key(), bound64) < 0); storage.Next())
            {
                _rocks.Delete(RocksDbManager.CF_STATE_STORAGE, storage.Key());
                if ((++deleted & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            }
            return deleted;
        }

        private Account DecodeAccount(byte[] accountHash, byte[] flatValue)
        {
            var account = _flat.DecodeFlatAccountValue(flatValue)
                ?? throw new InvalidOperationException($"snap.generate: flat account 0x{accountHash.ToHex()} does not decode");
            if (account.CodeHash == null && _flat.HasExternalCodeHashLayout)
                account.CodeHash = _flat.GetFlatCodeHash(accountHash);
            account.CodeHash ??= DefaultValues.EMPTY_DATA_HASH;
            return account;
        }

        private static byte[] NextAccountKey(Iterator accounts, byte[] shardEnd)
        {
            for (; accounts.Valid(); accounts.Next())
            {
                var key = accounts.Key();
                if (key.Length != 32) continue;
                return shardEnd != null && Compare(key, shardEnd) >= 0 ? null : key;
            }
            return null;
        }

        private static bool IsStorageOf(Iterator storage, byte[] accountHash)
        {
            if (!storage.Valid()) return false;
            var key = storage.Key();
            if (key.Length < 32) return false;
            for (int i = 0; i < 32; i++)
                if (key[i] != accountHash[i]) return false;
            return true;
        }

        private static (byte[] Start, byte[] End) ShardBounds(int shard)
        {
            var start = new byte[32];
            start[0] = (byte)(shard << 4);
            if (shard == ShardCount - 1) return (start, null);
            var end = new byte[32];
            end[0] = (byte)((shard + 1) << 4);
            return (start, end);
        }

        private static byte[] OwnerLowBound(byte[] owner32)
        {
            var bound = new byte[64];
            Buffer.BlockCopy(owner32, 0, bound, 0, 32);
            return bound;
        }

        private static int Compare(byte[] a, byte[] b) => ByteArrayComparer.Current.Compare(a, b);

        private sealed class AccountTrieBuilder
        {
            private readonly object _gate = new object();
            private readonly PatriciaTrie _trie;
            private readonly int _collapseInterval;
            private int _sinceCollapse;

            public AccountTrieBuilder(PatriciaTrie trie, int collapseInterval)
            {
                _trie = trie;
                _collapseInterval = collapseInterval;
            }

            public void Put(byte[] accountHash, byte[] encodedAccount)
            {
                lock (_gate)
                {
                    _trie.Put(accountHash, encodedAccount);
                    if (++_sinceCollapse < _collapseInterval) return;
                    _trie.SaveDirtyNodesToStorageAndCollapse();
                    _sinceCollapse = 0;
                }
            }

            public byte[] Commit()
            {
                lock (_gate)
                {
                    _trie.SaveDirtyNodesToStorage();
                    return _trie.Root.GetHash();
                }
            }
        }

        private sealed class ShardCounters
        {
            public long AccountsScanned;
            public long SlotsScanned;
            public long AccountRootsRewritten;
            public long DanglingSlotsDeleted;

            public static ShardCounters Sum(ShardCounters[] shards)
            {
                var total = new ShardCounters();
                foreach (var shard in shards)
                {
                    total.AccountsScanned += shard.AccountsScanned;
                    total.SlotsScanned += shard.SlotsScanned;
                    total.AccountRootsRewritten += shard.AccountRootsRewritten;
                    total.DanglingSlotsDeleted += shard.DanglingSlotsDeleted;
                }
                return total;
            }
        }
    }
}
