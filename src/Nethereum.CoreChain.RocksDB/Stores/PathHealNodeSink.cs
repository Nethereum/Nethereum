using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class PathHealNodeSink : IHealNodeSink, ISnapFlatStateWriter
    {
        private static readonly IHashProvider Keccak = Sha3KeccackHashProvider.Instance;
        private const int OwnerLength = 32;

        private readonly RocksDbPathTrieNodeStore _store;
        private readonly RocksDbManager _manager;
        private readonly ISnapFlatStateWriter _forward;
        private bool _generating;

        private readonly Dictionary<string, (bool IsStorage, byte[] Key, byte[] Blob)> _buffer = new();

        public PathHealNodeSink(RocksDbPathTrieNodeStore store, RocksDbManager manager)
            : this(store, manager, forward: null)
        {
        }

        internal PathHealNodeSink(RocksDbPathTrieNodeStore store, RocksDbManager manager, ISnapFlatStateWriter forward)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _forward = forward;
            _generating = CertifyJournal.Exists(manager);
        }

        public bool HasNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
            => Probe(isStorage, accountHash, nibblePath, expectedHash) == HealNodePresence.Match;

        public HealNodePresence Probe(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
        {
            var blob = ReadCurrent(isStorage, accountHash, nibblePath);
            if (blob == null || blob.Length < 32) return HealNodePresence.Absent;
            return ByteUtil.AreEqual(Keccak.ComputeHash(blob), expectedHash)
                ? HealNodePresence.Match
                : HealNodePresence.Stale;
        }

        public void PutNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash, byte[] blob)
        {
            if (blob == null || blob.Length < 32) return;
            var key = MakeKey(isStorage, accountHash, nibblePath);
            _buffer[BufferKey(isStorage, key)] = (isStorage, key, blob);
        }

        public void DeleteNode(bool isStorage, byte[] accountHash, byte[] nibblePath)
        {
            if (ReadCurrent(isStorage, accountHash, nibblePath) == null) return;
            var key = MakeKey(isStorage, accountHash, nibblePath);
            _buffer[BufferKey(isStorage, key)] = (isStorage, key, null);
        }

        private readonly List<byte[]> _pendingWipes = new();
        private readonly HashSet<string> _pendingWipeOwners = new(StringComparer.Ordinal);

        public void WipeStorage(byte[] accountHash)
        {
            WipeStorage(accountHash, force: false);
        }

        public void ForceWipeStorage(byte[] accountHash)
        {
            WipeStorage(accountHash, force: true);
        }

        private void WipeStorage(byte[] accountHash, bool force)
        {
            if (accountHash == null || accountHash.Length == 0) return;
            var ownerHex = accountHash.ToHex();

            var prefix = "s:" + ownerHex;
            var stale = new List<string>();
            foreach (var k in _buffer.Keys)
                if (k.StartsWith(prefix, StringComparison.Ordinal)) stale.Add(k);
            foreach (var k in stale) _buffer.Remove(k);

            var occupied = force
                || stale.Count > 0
                || _store.TryGetRaw(accountHash, Array.Empty<byte>()) != null;
            if (!occupied) return;

            if (_pendingWipeOwners.Add(ownerHex)) _pendingWipes.Add(accountHash);
        }

        public bool HasRoot(byte[] root)
            => TrieNodeVerification.RootBlobHashesTo(
                ReadCurrent(isStorage: false, accountHash: null, nibblePath: Array.Empty<byte>()), root);

        private const int ReadBackSampleInterval = 1024;
        private long _writesSinceSample = -1;

        public void Flush()
        {
            _generating = CertifyJournal.Exists(_manager);
            if (_buffer.Count == 0 && _pendingWipes.Count == 0) return;
            (bool IsStorage, byte[] Key, byte[] Blob)? sample;
            using (var batch = _manager.CreateWriteBatch())
            {
                if (_generating) CapturePreImages(batch);
                sample = AppendNodeChanges(batch);
                _manager.Write(batch);
            }
            _buffer.Clear();
            _pendingWipes.Clear();
            _pendingWipeOwners.Clear();
            _store.ClearCache();
            if (sample is { } s) VerifyReadBack(s);
        }

        private void CapturePreImages(WriteBatch batch)
        {
            var journal = _manager.GetColumnFamily(RocksDbManager.CF_FLAT_GENERATION);
            var capturedThisFlush = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _buffer.Values)
            {
                if (!capturedThisFlush.Add(BufferKey(entry.IsStorage, entry.Key))) continue;
                var (owner, path) = OwnerAndPath(entry.IsStorage, entry.Key);
                CertifyJournal.CaptureIfAbsent(
                    _manager, batch, journal, entry.IsStorage, owner, path, _store.TryGetRaw(owner, path));
            }
            foreach (var owner in _pendingWipes)
                if (capturedThisFlush.Add("w:" + owner.ToHex()))
                    CertifyJournal.CaptureWipeIfAbsent(_manager, batch, journal, owner);
        }

        private (bool IsStorage, byte[] Key, byte[] Blob)? AppendNodeChanges(WriteBatch batch)
        {
            var acctCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            (bool IsStorage, byte[] Key, byte[] Blob)? sample = null;
            foreach (var owner in _pendingWipes)
            {
                var upper = RocksDbPathTrieNodeStore.OwnerRangeEnd(owner);
                batch.DeleteRange(owner, (ulong)owner.Length, upper, (ulong)upper.Length, storCf);
            }
            foreach (var entry in _buffer.Values)
            {
                if (entry.Blob == null)
                {
                    batch.Delete(entry.Key, entry.IsStorage ? storCf : acctCf);
                    continue;
                }
                batch.Put(entry.Key, entry.Blob, entry.IsStorage ? storCf : acctCf);
                if (++_writesSinceSample % ReadBackSampleInterval == 0) sample = entry;
            }
            return sample;
        }

        private void VerifyReadBack((bool IsStorage, byte[] Key, byte[] Blob) sample)
        {
            var readBack = _manager.Get(
                sample.IsStorage ? RocksDbManager.CF_STATE_TRIE_STORAGE : RocksDbManager.CF_STATE_TRIE_ACCOUNT,
                sample.Key);
            if (readBack == null || !ByteUtil.AreEqual(readBack, sample.Blob))
                throw new InvalidOperationException(
                    "Path-keyed heal read-back sample mismatch: a just-flushed node is absent or altered at its location — keying or column-family wiring is broken.");
        }

        private bool DropsFlatWrites => _generating || _forward == null;

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => _forward?.GetAccountByHashAsync(accountHash) ?? Task.FromResult<Account>(null);

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            => DropsFlatWrites ? Task.CompletedTask : _forward.SaveAccountByHashAsync(accountHash, account);

        public Task DeleteAccountByHashAsync(byte[] accountHash)
            => DropsFlatWrites ? Task.CompletedTask : _forward.DeleteAccountByHashAsync(accountHash);

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            => DropsFlatWrites ? Task.CompletedTask : _forward.SaveStorageByHashAsync(accountHash, slotKeccak, value);

        private byte[] ReadCurrent(bool isStorage, byte[] accountHash, byte[] nibblePath)
        {
            var key = MakeKey(isStorage, accountHash, nibblePath);
            if (_buffer.TryGetValue(BufferKey(isStorage, key), out var buffered))
                return buffered.Blob;
            if (isStorage && _pendingWipeOwners.Contains(accountHash.ToHex()))
                return null;
            return _store.TryGetRaw(isStorage ? accountHash : Array.Empty<byte>(), nibblePath);
        }

        private static (byte[] Owner, byte[] Path) OwnerAndPath(bool isStorage, byte[] key)
        {
            if (!isStorage) return (Array.Empty<byte>(), key);
            var owner = new byte[OwnerLength];
            var path = new byte[key.Length - OwnerLength];
            Buffer.BlockCopy(key, 0, owner, 0, OwnerLength);
            Buffer.BlockCopy(key, OwnerLength, path, 0, path.Length);
            return (owner, path);
        }

        private static byte[] MakeKey(bool isStorage, byte[] accountHash, byte[] nibblePath)
            => isStorage
                ? TrieTracer.Key(accountHash, nibblePath)
                : nibblePath ?? Array.Empty<byte>();

        private static string BufferKey(bool isStorage, byte[] key)
            => (isStorage ? "s:" : "a:") + key.ToHex();
    }
}
