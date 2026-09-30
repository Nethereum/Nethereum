using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FlatStateReconciler : IFlatStateReconciler
    {
        private readonly RocksDbManager _rocks;
        private readonly RocksDbStateStore _flat;
        private readonly ITrieNodeStore _trieNodes;
        private readonly TimeProvider _clock;

        private static readonly byte[] ZeroKey32 = new byte[32];
        private const long ProgressAccountInterval = 1_000_000;
        private const long ProgressSlotInterval = 10_000_000;
        private static readonly TimeSpan ProgressTimeInterval = TimeSpan.FromSeconds(60);

        public FlatStateReconciler(
            RocksDbManager rocks, RocksDbStateStore flatStore, ITrieNodeStore trieNodes, TimeProvider clock = null)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _flat = flatStore ?? throw new ArgumentNullException(nameof(flatStore));
            _trieNodes = trieNodes ?? throw new ArgumentNullException(nameof(trieNodes));
            _clock = clock ?? TimeProvider.System;
        }

        public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct)
            => Run(stateRoot, progress, ct, dryRun: false);

        public Task<FlatStateReconcileResult> VerifyFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            => Run(stateRoot, progress, ct, dryRun: true, sampleAccountsPerShard);

        private const int ShardCount = 16;
        private const int MaxUnresolvableCollected = 65_536;

        private Task<FlatStateReconcileResult> Run(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, bool dryRun, long sampleAccountsPerShard = 0)
        {
            if (stateRoot == null || stateRoot.Length != 32)
                throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            return Task.Run(async () =>
            {
                var tasks = new Task<ShardResult>[ShardCount];
                for (int i = 0; i < ShardCount; i++)
                {
                    int shard = i;
                    var start = new byte[32];
                    start[0] = (byte)(shard << 4);
                    byte[] end = null;
                    if (shard < ShardCount - 1)
                    {
                        end = new byte[32];
                        end[0] = (byte)((shard + 1) << 4);
                    }
                        if (!dryRun && HasCleanShardMarker(stateRoot, shard))
                    {
                        tasks[shard] = Task.FromResult(new ShardResult
                        {
                            Result = new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0),
                            Unresolvable = new System.Collections.Generic.List<(byte[], byte[])>(),
                            MissingCode = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current),
                        });
                        continue;
                    }
                    tasks[shard] = Task.Run(
                        () => ReconcileShard(stateRoot, start, end, shard, progress, ct, dryRun, sampleAccountsPerShard), ct);
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);

                long accounts = 0, slots = 0, gA = 0, gS = 0, aA = 0, aS = 0, pA = 0, pS = 0;
                var unresolvable = new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>();
                foreach (var t in tasks)
                {
                    var r = t.Result;
                    accounts += r.Result.AccountsScanned; slots += r.Result.SlotsScanned;
                    gA += r.Result.GhostAccountsDeleted; gS += r.Result.GhostSlotsDeleted;
                    aA += r.Result.AccountsAdded; aS += r.Result.SlotsAdded;
                    pA += r.Result.AccountsPatched; pS += r.Result.SlotsPatched;
                    unresolvable.AddRange(r.Unresolvable);
                }
                if (!dryRun)
                {
                    for (int i = 0; i < ShardCount; i++)
                        if (tasks[i].Result.Unresolvable.Count == 0)
                            WriteCleanShardMarker(stateRoot, i);
                }

                var missingCodeAll = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
                foreach (var t in tasks) missingCodeAll.UnionWith(t.Result.MissingCode);
                PersistMissingCode(missingCodeAll);

                if (unresolvable.Count > 0)
                {
                    var totalDamaged = unresolvable.Count;
                    if (unresolvable.Count > MaxUnresolvableCollected)
                        unresolvable.RemoveRange(MaxUnresolvableCollected, unresolvable.Count - MaxUnresolvableCollected);
                    PersistDamage(unresolvable);
                    throw new FlatReconcileUnresolvableNodeException(
                        $"Flat reconcile: {totalDamaged} unresolvable storage subtree(s) ({unresolvable.Count} carried for repair) — first at account " +
                        $"0x{ToHex(unresolvable[0].AccountHash)} (storageRoot 0x{ToHex(unresolvable[0].StorageRoot)}); " +
                        "each needs a wipe + seeded re-download (ancestors are hash-intact, so descent walks prune over the damage).",
                        unresolvable, null);
                }
                return new FlatStateReconcileResult(accounts, slots, gA, gS, aA, aS, pA, pS);
            }, ct);
        }

        private bool IsProgressDue(long scanned, long countInterval, ref long lastReportedScanned, ref DateTimeOffset lastReportedAt)
        {
            if (scanned - lastReportedScanned >= countInterval)
            {
                lastReportedScanned = scanned;
                lastReportedAt = _clock.GetUtcNow();
                return true;
            }
            var now = _clock.GetUtcNow();
            if (now - lastReportedAt < ProgressTimeInterval)
                return false;
            lastReportedScanned = scanned;
            lastReportedAt = now;
            return true;
        }

        private sealed class ShardResult
        {
            public FlatStateReconcileResult Result;
            public System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)> Unresolvable;
            public System.Collections.Generic.HashSet<byte[]> MissingCode;
        }

        private ShardResult ReconcileShard(
            byte[] stateRoot, byte[] shardStart, byte[] shardEnd, int shardIdx,
            Action<string> progress, CancellationToken ct, bool dryRun, long sampleAccountsPerShard = 0)
        {
            var trieReader = new StrictNodeReadStore(_trieNodes);
            var decoder = new AccountEncoder();
            var sw = Stopwatch.StartNew();
            long lastReportedAccounts = 0;
            long lastReportedSlots = 0;
            var lastReportedAt = _clock.GetUtcNow();

            long accountsScanned = 0, slotsScanned = 0;
            long ghostAccounts = 0, ghostSlots = 0;
            long accountsAdded = 0, slotsAdded = 0;
            long accountsPatched = 0, slotsPatched = 0;

            var unresolvable = new System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)>();
            var codeSeen = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            var missingCode = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            bool sampleCapped = false;

            try
            {
            var trie = PatriciaTrie.LoadFromStorage(stateRoot, trieReader);
            using (var acctIt = _rocks.CreateIterator(RocksDbManager.CF_STATE_ACCOUNTS))
            using (var storIt = _rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE))
            {
                var (shardStart64, shardEnd64) = PadShardBoundsTo64(shardStart, shardEnd);
                acctIt.Seek(shardStart);
                storIt.Seek(shardStart64);

                byte[] NextPrimaryAccountKey()
                {
                    while (acctIt.Valid())
                    {
                        var k = acctIt.Key();
                        if (k.Length == 32)
                            return shardEnd != null && CompareBytes(k, shardEnd) >= 0 ? null : k;
                        acctIt.Next();
                    }
                    return null;
                }

                void DrainGhostStorageBelow(byte[] bound64)
                {
                    while (storIt.Valid())
                    {
                        var k = storIt.Key();
                        if (shardEnd64 != null && CompareBytes(k, shardEnd64) >= 0) break;
                        if (bound64 != null && CompareBytes(k, bound64) >= 0) break;
                        if (!dryRun) DeleteStorageRow(k);
                        if ((++ghostSlots & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                        storIt.Next();
                    }
                }

                void SkipStorageOfOwner(byte[] owner32)
                {
                    while (storIt.Valid())
                    {
                        var k = storIt.Key();
                        if (!HasOwnerPrefix(k, owner32)) break;
                        storIt.Next();
                    }
                }

                void DrainGhostStorageOfOwner(byte[] owner32)
                {
                    while (storIt.Valid())
                    {
                        var k = storIt.Key();
                        if (!HasOwnerPrefix(k, owner32)) break;
                        if (!dryRun) DeleteStorageRow(k);
                        if ((++ghostSlots & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                        storIt.Next();
                    }
                }

                foreach (var leaf in PatriciaRangeIterator.EnumerateRange(trie.Root, trieReader, shardStart))
                {
                    ct.ThrowIfCancellationRequested();
                    var accountKey = leaf.KeyBytes;
                    if (shardEnd != null && CompareBytes(accountKey, shardEnd) >= 0) break;
                    accountsScanned++;

                    if (sampleAccountsPerShard > 0 && accountsScanned > sampleAccountsPerShard)
                    {
                        sampleCapped = true;
                        break;
                    }

                    byte[] flatKey;
                    while ((flatKey = NextPrimaryAccountKey()) != null && CompareBytes(flatKey, accountKey) < 0)
                    {
                        if (!dryRun) _flat.DeleteFlatAccountByHash(flatKey);
                        ghostAccounts++;
                        acctIt.Next();
                    }

                    var trieAccount = decoder.Decode(leaf.Value);

                    CollectMissingCode(trieAccount, codeSeen, missingCode);

                    ReconcileAccountRow(acctIt, flatKey, accountKey, trieAccount, dryRun,
                        ref accountsPatched, ref accountsAdded);

                    DrainGhostStorageBelow(OwnerLowBound(accountKey));

                    if (HasStorage(trieAccount))
                    {
                        try
                        {
                        var storageTrie = PatriciaTrie.LoadFromStorage(trieAccount.StateRoot, trieReader, accountKey);
                        foreach (var slotLeaf in PatriciaRangeIterator.EnumerateRange(storageTrie.Root, trieReader, ZeroKey32))
                        {
                            slotsScanned++;
                            if ((slotsScanned & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                            if (progress != null && IsProgressDue(slotsScanned, ProgressSlotInterval, ref lastReportedSlots, ref lastReportedAt))
                                progress($"shard={shardIdx:X} accounts={accountsScanned:N0} slots={slotsScanned:N0} " +
                                         $"repairs={ghostAccounts + ghostSlots + accountsAdded + slotsAdded + accountsPatched + slotsPatched:N0} " +
                                         $"elapsed={sw.Elapsed:hh\\:mm\\:ss}");

                            var slotFlatKey = ConcatOwnerSlot(accountKey, slotLeaf.KeyBytes);
                            while (storIt.Valid() && HasOwnerPrefix(storIt.Key(), accountKey)
                                   && CompareBytes(storIt.Key(), slotFlatKey) < 0)
                            {
                                if (!dryRun) DeleteStorageRow(storIt.Key());
                                if ((++ghostSlots & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                                storIt.Next();
                            }

                            var trieValue = Nethereum.RLP.RLP.Decode(slotLeaf.Value).RLPData;
                            if (storIt.Valid() && CompareBytes(storIt.Key(), slotFlatKey) == 0)
                            {
                                if (!BytesEqual(storIt.Value(), trieValue))
                                {
                                    if (!dryRun) _flat.SaveStorageByHashAsync(accountKey, slotLeaf.KeyBytes, trieValue).GetAwaiter().GetResult();
                                    slotsPatched++;
                                }
                                storIt.Next();
                            }
                            else
                            {
                                if (!dryRun) _flat.SaveStorageByHashAsync(accountKey, slotLeaf.KeyBytes, trieValue).GetAwaiter().GetResult();
                                slotsAdded++;
                            }
                        }
                        DrainGhostStorageOfOwner(accountKey);
                        }
                        catch (InvalidOperationException ex) when (ex is not FlatReconcileUnresolvableNodeException)
                        {
                            unresolvable.Add((accountKey, trieAccount.StateRoot));
                            SkipStorageOfOwner(accountKey);
                        }
                    }
                    else
                    {
                        DrainGhostStorageOfOwner(accountKey);
                    }

                    if (progress != null && IsProgressDue(accountsScanned, ProgressAccountInterval, ref lastReportedAccounts, ref lastReportedAt))
                    {
                        progress($"shard={shardIdx:X} accounts={accountsScanned:N0} slots={slotsScanned:N0} " +
                                 $"repairs={ghostAccounts + ghostSlots + accountsAdded + slotsAdded + accountsPatched + slotsPatched:N0} " +
                                 $"elapsed={sw.Elapsed:hh\\:mm\\:ss}");
                    }
                }

                if (!sampleCapped)
                {
                    byte[] tailKey;
                    while ((tailKey = NextPrimaryAccountKey()) != null)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!dryRun) _flat.DeleteFlatAccountByHash(tailKey);
                        ghostAccounts++;
                        acctIt.Next();
                    }
                    DrainGhostStorageBelow(shardEnd64);
                }
            }
            }
            catch (InvalidOperationException ex) when (ex is not FlatReconcileUnresolvableNodeException)
            {
                unresolvable.Add((FlatReconcileUnresolvableNodeException.WholeAccountTrieSentinel, stateRoot));
            }

            return new ShardResult
            {
                Result = new FlatStateReconcileResult(
                    accountsScanned, slotsScanned,
                    ghostAccounts, ghostSlots,
                    accountsAdded, slotsAdded,
                    accountsPatched, slotsPatched),
                Unresolvable = unresolvable,
                MissingCode = missingCode,
            };
        }

        private static (byte[] shardStart64, byte[] shardEnd64) PadShardBoundsTo64(byte[] shardStart, byte[] shardEnd)
        {
            var shardStart64 = new byte[64];
            Buffer.BlockCopy(shardStart, 0, shardStart64, 0, 32);
            byte[] shardEnd64 = null;
            if (shardEnd != null)
            {
                shardEnd64 = new byte[64];
                Buffer.BlockCopy(shardEnd, 0, shardEnd64, 0, 32);
            }
            return (shardStart64, shardEnd64);
        }

        private void CollectMissingCode(
            Account trieAccount,
            System.Collections.Generic.HashSet<byte[]> codeSeen,
            System.Collections.Generic.HashSet<byte[]> missingCode)
        {
            if (trieAccount?.CodeHash is { Length: 32 } codeHash
                && !BytesEqual(codeHash, DefaultValues.EMPTY_DATA_HASH)
                && codeSeen.Add(codeHash)
                && _flat.GetCodeAsync(codeHash).GetAwaiter().GetResult() is not { Length: > 0 })
            {
                missingCode.Add(codeHash);
            }
        }

        private void ReconcileAccountRow(
            RocksDbSharp.Iterator acctIt, byte[] flatKey, byte[] accountKey, Account trieAccount, bool dryRun,
            ref long accountsPatched, ref long accountsAdded)
        {
            if (flatKey != null && CompareBytes(flatKey, accountKey) == 0)
            {
                var flatAccount = _flat.DecodeFlatAccountValue(acctIt.Value());
                if (flatAccount != null && flatAccount.CodeHash == null && _flat.HasExternalCodeHashLayout)
                    flatAccount.CodeHash = _flat.GetFlatCodeHash(accountKey);
                if (!AccountsEqual(flatAccount, trieAccount))
                {
                    if (!dryRun) _flat.SaveAccountByHashAsync(accountKey, trieAccount).GetAwaiter().GetResult();
                    accountsPatched++;
                }
                acctIt.Next();
            }
            else
            {
                if (!dryRun) _flat.SaveAccountByHashAsync(accountKey, trieAccount).GetAwaiter().GetResult();
                accountsAdded++;
            }
        }

        private static byte[] DamageInventoryKey => System.Text.Encoding.ASCII.GetBytes("flatrepair:inventory");

        private static byte[] CleanShardKey(byte[] root, int shard)
        {
            var key = new byte[15 + 32 + 1];
            System.Text.Encoding.ASCII.GetBytes("flatrecon:clean").CopyTo(key, 0);
            root.CopyTo(key, 15);
            key[47] = (byte)shard;
            return key;
        }

        private bool HasCleanShardMarker(byte[] root, int shard)
            => _rocks.Get(RocksDbManager.CF_METADATA, CleanShardKey(root, shard)) != null;

        private void WriteCleanShardMarker(byte[] root, int shard)
            => _rocks.Put(RocksDbManager.CF_METADATA, CleanShardKey(root, shard), new byte[] { 1 });

        private void PersistDamage(System.Collections.Generic.List<(byte[] AccountHash, byte[] StorageRoot)> damage)
        {
            var blob = new byte[damage.Count * 64];
            for (int i = 0; i < damage.Count; i++)
            {
                damage[i].AccountHash.CopyTo(blob, i * 64);
                damage[i].StorageRoot.CopyTo(blob, i * 64 + 32);
            }
            _rocks.Put(RocksDbManager.CF_METADATA, DamageInventoryKey, blob);
        }

        public System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage()
        {
            var blob = _rocks.Get(RocksDbManager.CF_METADATA, DamageInventoryKey);
            var list = new System.Collections.Generic.List<(byte[], byte[])>();
            if (blob == null || blob.Length % 64 != 0) return list;
            for (int i = 0; i + 64 <= blob.Length; i += 64)
            {
                var acct = new byte[32];
                var root = new byte[32];
                Buffer.BlockCopy(blob, i, acct, 0, 32);
                Buffer.BlockCopy(blob, i + 32, root, 0, 32);
                list.Add((acct, root));
            }
            return list;
        }

        public void ClearPersistedDamage()
            => _rocks.Delete(RocksDbManager.CF_METADATA, DamageInventoryKey);

        public void ClearCleanShardMarkers(byte[] root)
        {
            if (root == null || root.Length != 32) return;
            for (int shard = 0; shard < ShardCount; shard++)
                _rocks.Delete(RocksDbManager.CF_METADATA, CleanShardKey(root, shard));
        }

        private static byte[] MissingCodeKey => System.Text.Encoding.ASCII.GetBytes("flatrepair:missingcode");

        private void PersistMissingCode(System.Collections.Generic.HashSet<byte[]> hashes)
        {
            if (hashes.Count == 0)
            {
                _rocks.Delete(RocksDbManager.CF_METADATA, MissingCodeKey);
                return;
            }
            var blob = new byte[hashes.Count * 32];
            int i = 0;
            foreach (var h in hashes) { h.CopyTo(blob, i * 32); i++; }
            _rocks.Put(RocksDbManager.CF_METADATA, MissingCodeKey, blob);
        }

        public System.Collections.Generic.IReadOnlyList<byte[]> GetPersistedMissingCode()
        {
            var blob = _rocks.Get(RocksDbManager.CF_METADATA, MissingCodeKey);
            var list = new System.Collections.Generic.List<byte[]>();
            if (blob == null || blob.Length % 32 != 0) return list;
            for (int i = 0; i + 32 <= blob.Length; i += 32)
            {
                var h = new byte[32];
                Buffer.BlockCopy(blob, i, h, 0, 32);
                list.Add(h);
            }
            return list;
        }

        public void ClearPersistedMissingCode()
            => _rocks.Delete(RocksDbManager.CF_METADATA, MissingCodeKey);

        private void DeleteStorageRow(byte[] flatKey64)
        {
            _rocks.Delete(RocksDbManager.CF_STATE_STORAGE, flatKey64);
        }

        private static bool HasStorage(Account account)
            => account?.StateRoot != null
               && account.StateRoot.Length == 32
               && !BytesEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH);

        private static bool AccountsEqual(Account flat, Account trie)
        {
            if (flat == null || trie == null) return false;
            return flat.Nonce == trie.Nonce
                && flat.Balance == trie.Balance
                && HashesEqual(flat.StateRoot, trie.StateRoot, DefaultValues.EMPTY_TRIE_HASH)
                && HashesEqual(flat.CodeHash, trie.CodeHash, DefaultValues.EMPTY_DATA_HASH);
        }

        private static bool HashesEqual(byte[] a, byte[] b, byte[] emptyDefault)
            => BytesEqual(a ?? emptyDefault, b ?? emptyDefault);

        private static byte[] OwnerLowBound(byte[] owner32)
        {
            var bound = new byte[64];
            Buffer.BlockCopy(owner32, 0, bound, 0, 32);
            return bound;
        }

        private static byte[] ConcatOwnerSlot(byte[] owner32, byte[] slot32)
        {
            var key = new byte[64];
            Buffer.BlockCopy(owner32, 0, key, 0, 32);
            Buffer.BlockCopy(slot32, 0, key, 32, 32);
            return key;
        }

        private static bool HasOwnerPrefix(byte[] key64, byte[] owner32)
        {
            if (key64 == null || key64.Length < 32) return false;
            for (int i = 0; i < 32; i++)
                if (key64[i] != owner32[i]) return false;
            return true;
        }

        private sealed class StrictNodeReadStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;

            public StrictNodeReadStore(ITrieNodeStore inner) { _inner = inner; }

            public byte[] Get(Node reference)
            {
                var blob = _inner.Get(reference);
                if (blob == null || blob.Length == 0)
                    throw new InvalidOperationException(
                        "Flat reconcile: trie node unresolvable while enumerating a converged trie — " +
                        "aborting the pass (a truncated leaf stream would delete correct flat rows).");
                return blob;
            }

            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Commit(TrieNodeSet nodes) => throw new NotSupportedException("reconcile is read-only over the trie");
            public void Flush() { }
            public void Clear() => throw new NotSupportedException("reconcile is read-only over the trie");
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return "(null)";
            var c = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++) { c[i * 2] = hex[bytes[i] >> 4]; c[i * 2 + 1] = hex[bytes[i] & 0xF]; }
            return new string(c);
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                int d = a[i] - b[i];
                if (d != 0) return d;
            }
            return a.Length - b.Length;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
