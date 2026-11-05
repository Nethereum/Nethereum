using System;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal static class CertifyJournal
    {
        internal const string GenerationCf = RocksDbManager.CF_FLAT_GENERATION;
        internal const byte UnitDoneTag = 0x02;
        internal const byte CaptureTag = 0x03;
        internal const byte WipeTag = 0x04;
        private const byte AccountTrieTag = 0x00;
        internal const byte StorageTrieTag = 0x01;
        private static readonly byte[] JournalRootKey = { 0x01 };
        private static readonly byte[] AbsentPreImage = { 0x00 };
        private static readonly byte[] CapturesStart = { CaptureTag };
        private static readonly byte[] CapturesEnd = { WipeTag + 1 };
        internal static readonly byte[] JournalKeysStart = { 0x00 };
        internal static readonly byte[] JournalKeysEnd = { 0xFF };

        internal static bool Exists(RocksDbManager rocks) => JournalRoot(rocks) != null;

        internal static byte[] JournalRoot(RocksDbManager rocks) => rocks.Get(GenerationCf, JournalRootKey);

        internal static void StartIfAbsent(RocksDbManager rocks, byte[] root)
        {
            if (Exists(rocks)) return;
            var cf = rocks.GetColumnFamily(GenerationCf);
            using var batch = rocks.CreateWriteBatch();
            DeleteAll(batch, cf);
            batch.Put(JournalRootKey, root, cf);
            rocks.Write(batch);
        }

        internal static bool IsUnitDone(RocksDbManager rocks, int unit) => rocks.Get(GenerationCf, UnitKey(unit)) != null;

        internal static void MarkUnitDone(RocksDbManager rocks, int unit)
            => rocks.Put(GenerationCf, UnitKey(unit), Array.Empty<byte>());

        internal static void ClearUnit(RocksDbManager rocks, int unit) => rocks.Delete(GenerationCf, UnitKey(unit));

        internal static (bool Captured, byte[] Blob) PreImage(RocksDbManager rocks, byte[] owner, byte[] path)
        {
            var isStorage = owner is { Length: > 0 };
            var captured = rocks.Get(GenerationCf, CaptureKey(isStorage, owner, path));
            if (captured != null) return (true, ByteUtil.AreEqual(captured, AbsentPreImage) ? null : captured);
            return isStorage && rocks.Get(GenerationCf, WipeKey(owner)) != null ? (true, null) : (false, null);
        }

        internal static void CaptureIfAbsent(
            RocksDbManager rocks, WriteBatch batch, ColumnFamilyHandle cf, bool isStorage, byte[] owner, byte[] path, byte[] currentBlob)
        {
            var key = CaptureKey(isStorage, owner, path);
            if (rocks.Get(GenerationCf, key) == null) batch.Put(key, currentBlob ?? AbsentPreImage, cf);
        }

        internal static void CaptureWipeIfAbsent(RocksDbManager rocks, WriteBatch batch, ColumnFamilyHandle cf, byte[] owner)
        {
            var key = WipeKey(owner);
            if (rocks.Get(GenerationCf, key) == null) batch.Put(key, Array.Empty<byte>(), cf);
        }

        internal static void CloseEpoch(RocksDbManager rocks, byte[] root)
        {
            var cf = rocks.GetColumnFamily(GenerationCf);
            using (var batch = rocks.CreateWriteBatch())
            {
                batch.Put(JournalRootKey, root, cf);
                batch.DeleteRange(CapturesStart, (ulong)CapturesStart.Length, CapturesEnd, (ulong)CapturesEnd.Length, cf);
                rocks.Write(batch);
            }
            rocks.CompactKeyRange(GenerationCf, CapturesStart, CapturesEnd);
        }

        internal static void DeleteAll(WriteBatch batch, ColumnFamilyHandle cf)
            => batch.DeleteRange(JournalKeysStart, (ulong)JournalKeysStart.Length, JournalKeysEnd, (ulong)JournalKeysEnd.Length, cf);

        internal static void Discard(RocksDbManager rocks)
        {
            using (var batch = rocks.CreateWriteBatch())
            {
                DeleteAll(batch, rocks.GetColumnFamily(GenerationCf));
                rocks.Write(batch);
            }
            rocks.CompactKeyRange(GenerationCf, JournalKeysStart, JournalKeysEnd);
        }

        internal static bool TrieChangedSince(RocksDbManager rocks, byte[] root)
            => !ByteUtil.AreEqual(JournalRoot(rocks), root) || AnyKey(rocks, CaptureTag) || AnyKey(rocks, WipeTag);

        internal static void DiscardAtRoot(RocksDbManager rocks, byte[] root)
        {
            if (root == null || root.Length != 32) return;
            if (ByteUtil.AreEqual(JournalRoot(rocks), root))
                Discard(rocks);
            for (int shard = 0; shard < FlatStateWalk.ShardCount; shard++)
                rocks.Delete(RocksDbManager.CF_METADATA, CleanShardKey(root, shard));
        }

        internal static bool AnyKey(RocksDbManager rocks, byte tag)
        {
            using var it = rocks.CreateIterator(GenerationCf);
            it.Seek(new[] { tag });
            return it.Valid() && it.GetKeySpan()[0] == tag;
        }

        internal static long CountKeys(RocksDbManager rocks, byte tag)
        {
            long count = 0;
            using var it = rocks.CreateIterator(GenerationCf);
            for (it.Seek(new[] { tag }); it.Valid() && it.GetKeySpan()[0] == tag; it.Next()) count++;
            return count;
        }

        private static byte[] UnitKey(int unit) => new[] { UnitDoneTag, (byte)unit };

        internal static byte[] WipeKey(byte[] owner) => ByteUtil.Merge(new[] { WipeTag }, owner);

        internal static byte[] CaptureKey(bool isStorage, byte[] owner, byte[] path)
            => isStorage
                ? ByteUtil.Merge(new[] { CaptureTag, StorageTrieTag }, owner, path ?? Array.Empty<byte>())
                : ByteUtil.Merge(new[] { CaptureTag, AccountTrieTag }, path ?? Array.Empty<byte>());

        private static byte[] CleanShardKey(byte[] root, int shard)
            => ByteUtil.Merge(System.Text.Encoding.ASCII.GetBytes("flatrecon:clean"), root, new[] { (byte)shard });
    }
}
