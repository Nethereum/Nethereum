using System;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using RocksDbSharp;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.CoreChain.Storage;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB.Stores
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "RocksDbPathTrieNodeStore - the path-keyed (location-addressed) state-trie node store")]
    public class RocksDbPathTrieNodeStore : ITrieNodeStore, IContractStorageWipeable, IRawNodeReader, ICacheInvalidatable
    {
        private readonly RocksDbManager _manager;
        private static readonly IHashProvider _hashProvider = Sha3KeccackHashProvider.Instance;

        public RocksDbPathTrieNodeStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        private byte[] ReadRaw(byte[] owner, byte[] path)
        {
            return IsAccount(owner)
                ? _manager.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, path ?? Array.Empty<byte>())
                : _manager.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path));
        }

        public void ClearCache() { }

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null || (nodes.Count == 0 && nodes.Deletes.Count == 0)) return;
            using var batch = _manager.CreateWriteBatch();
            AppendCommit(batch, nodes);
            _manager.Write(batch);
        }

        internal void AppendCommit(WriteBatch batch, TrieNodeSet nodes)
        {
            if (nodes == null) return;
            var acctCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            foreach (var node in nodes.Nodes)
            {
                var rlp = node.GetEncodedData();
                if (rlp == null || rlp.Length < 32) continue;
                if (IsAccount(node.Owner))
                    batch.Put(node.Path ?? Array.Empty<byte>(), rlp, acctCf);
                else
                    batch.Put(StorageKey(node.Owner, node.Path), rlp, storCf);
            }
            foreach (var d in nodes.Deletes)
            {
                if (d.PrevBlob != null && d.PrevBlob.Length < 32) continue;
                if (IsAccount(d.Owner))
                    batch.Delete(d.Path ?? Array.Empty<byte>(), acctCf);
                else
                    batch.Delete(StorageKey(d.Owner, d.Path), storCf);
            }
        }

        internal byte[] TryGetRaw(byte[] owner, byte[] path)
        {
            return ReadRaw(owner, path);
        }

        public byte[] TryGetRawNode(byte[] owner, byte[] path) => TryGetRaw(owner, path);

        public byte[] Get(Node reference)
        {
            if (reference == null) return null;
            byte[] blob = ReadRaw(reference.Owner, reference.Path);
            if (!ProofVerification.Current.TrieNode.Verify(reference.GetHash(), blob, _hashProvider))
                throw new InvalidOperationException(
                    "Path-keyed trie node hash mismatch: stored blob does not match the parent-referenced hash (tampered or corrupt).");
            return blob;
        }

        public bool Contains(Node reference)
        {
            if (reference == null) return false;
            return IsAccount(reference.Owner)
                ? _manager.KeyExists(RocksDbManager.CF_STATE_TRIE_ACCOUNT, reference.Path ?? Array.Empty<byte>())
                : _manager.KeyExists(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(reference.Owner, reference.Path));
        }

        public bool ContainsKey(byte[] stateRoot)
        {
            if (stateRoot == null || stateRoot.Length != 32) return false;
            var blob = TryGetRaw(Array.Empty<byte>(), Array.Empty<byte>());
            if (blob == null || blob.Length == 0) return false;
            return ByteUtil.AreEqual(_hashProvider.ComputeHash(blob), stateRoot);
        }

        public void Flush() => _manager.Flush();

        public void Clear()
        {
            WipeCf(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            WipeCf(RocksDbManager.CF_STATE_TRIE_STORAGE);
        }

        public void DeleteRange(byte[] owner)
        {
            if (owner == null || owner.Length == 0) return;

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);

            iterator.Seek(owner);
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!ByteUtil.StartsWith(key, owner)) break;
                batch.Delete(key, cf);
                iterator.Next();
            }

            _manager.Write(batch);
        }

        private void WipeCf(string cfName)
        {
            using var iterator = _manager.CreateIterator(cfName);
            using var batch = _manager.CreateWriteBatch();
            var cf = _manager.GetColumnFamily(cfName);
            iterator.SeekToFirst();
            while (iterator.Valid()) { batch.Delete(iterator.Key(), cf); iterator.Next(); }
            _manager.Write(batch);
        }

        private static bool IsAccount(byte[] owner) => owner == null || owner.Length == 0;

        internal static byte[] StorageKey(byte[] owner, byte[] path)
        {
            var pathLen = path?.Length ?? 0;
            var key = new byte[owner.Length + pathLen];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, owner.Length, pathLen);
            return key;
        }
    }
}
