using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public readonly struct RangePageResult
    {
        public bool Accepted { get; }

        public byte[] Cursor { get; }

        public bool HasMore { get; }

        private RangePageResult(bool accepted, byte[] cursor, bool hasMore)
        {
            Accepted = accepted;
            Cursor = cursor;
            HasMore = hasMore;
        }

        public static RangePageResult Reject(byte[] cursorUnchanged) => new RangePageResult(false, cursorUnchanged, false);
        public static RangePageResult Accept(byte[] cursor, bool hasMore) => new RangePageResult(true, cursor, hasMore);
    }

    public sealed class ResumableStorageScope
    {
        private const int DefaultFlushIntervalSlots = 5_000;

        private readonly byte[] _accountHash;
        private readonly ISnapFlatStateWriter _flatWriter;
        private readonly int _flushIntervalSlots;
        private readonly object _slotLock = new();
        private PatriciaTrie _trie;
        private byte[] _cursor;
        private int _slotsSinceFlush;

        private ResumableStorageScope(
            byte[] accountHash, PatriciaTrie trie, byte[] resumeCursor, int flushIntervalSlots,
            ISnapFlatStateWriter flatWriter)
        {
            _accountHash = accountHash;
            _flatWriter = flatWriter;
            _trie = trie;
            _cursor = resumeCursor;
            _flushIntervalSlots = flushIntervalSlots;
        }

        public static ResumableStorageScope Open(
            ITrieNodeStore store, byte[] accountHash, byte[] resumeCursor = null, int flushIntervalSlots = DefaultFlushIntervalSlots,
            ISnapFlatStateWriter flatWriter = null)
        {
            if (store == null && flatWriter == null) throw new ArgumentNullException(nameof(store));
            if (flushIntervalSlots < 1) throw new ArgumentOutOfRangeException(nameof(flushIntervalSlots));

            if (store == null)
                return new ResumableStorageScope(accountHash, null, resumeCursor, flushIntervalSlots, flatWriter);

            var trie = store is IRawNodeReader rawReader
                ? PatriciaTrie.ReattachFromRawRoot(rawReader, store, accountHash)
                : null;

            if (trie == null && resumeCursor != null)
                throw new InvalidOperationException(
                    $"Resume cursor asserts prior storage progress for owner {accountHash.ToHex()} but no persisted root was recoverable — refusing to start fresh and silently drop the covered range.");

            trie ??= new PatriciaTrie(store, accountHash);

            return new ResumableStorageScope(accountHash, trie, resumeCursor, flushIntervalSlots, flatWriter);
        }

        public byte[] AccountHash => _accountHash;

        public byte[] Cursor { get { lock (_slotLock) return _cursor; } }

        public byte[] CurrentRootHash { get { lock (_slotLock) return _trie?.Root.GetHash(); } }

        public byte[] Get(byte[] slotHash)
        {
            lock (_slotLock)
            {
                if (_trie == null)
                    throw new InvalidOperationException(
                        $"Storage scope for owner {_accountHash.ToHex()} keeps no local trie; slots are written to flat state only.");
                return _trie.Get(slotHash);
            }
        }

        public RangePageResult ApplyVerifiedPage(
            byte[] storageRoot, byte[] firstKey, IList<byte[]> slotHashes, IList<byte[]> valuesRlp, IList<byte[]> proofNodes)
        {
            var proof = ProofVerification.Current.Range.Verify(storageRoot, firstKey, slotHashes, valuesRlp, proofNodes);
            if (!proof.Valid) return RangePageResult.Reject(Cursor);

            RangePageResult accepted;
            lock (_slotLock)
            {
                if (_trie != null) PutIntoTrie(slotHashes, valuesRlp);
                if (slotHashes.Count > 0)
                    _cursor = SnapHashRanges.IncrementHash(slotHashes[slotHashes.Count - 1]);
                accepted = RangePageResult.Accept(_cursor, proof.HasMore);
            }
            WriteFlat(slotHashes, valuesRlp);
            return accepted;
        }

        private void PutIntoTrie(IList<byte[]> slotHashes, IList<byte[]> valuesRlp)
        {
            for (int i = 0; i < slotHashes.Count; i++)
            {
                _trie.Put(slotHashes[i], valuesRlp[i]);
                if (++_slotsSinceFlush >= _flushIntervalSlots) CommitDirtyNodesLocked();
            }
        }

        private void WriteFlat(IList<byte[]> slotHashes, IList<byte[]> valuesRlp)
        {
            if (_flatWriter == null) return;
            for (int i = 0; i < slotHashes.Count; i++)
                _flatWriter.SaveStorageByHashAsync(
                        _accountHash, slotHashes[i], Nethereum.RLP.RLP.Decode(valuesRlp[i]).RLPData)
                    .GetAwaiter().GetResult();
        }

        public void CommitDirtyNodes()
        {
            lock (_slotLock)
            {
                if (_trie != null) CommitDirtyNodesLocked();
            }
        }

        private void CommitDirtyNodesLocked()
        {
            _trie.SaveDirtyNodesToStorageAndCollapse();
            _slotsSinceFlush = 0;
        }
    }
}
