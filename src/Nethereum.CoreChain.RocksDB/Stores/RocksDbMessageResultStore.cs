using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Messaging;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbMessageResultStore : IMessageResultStore
    {
        private readonly RocksDbManager _manager;

        public RocksDbMessageResultStore(RocksDbManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public Task StoreAsync(MessageResult result)
        {
            using var batch = _manager.CreateWriteBatch();
            var resultsCf = _manager.GetColumnFamily(RocksDbManager.CF_MSG_RESULTS);
            var byLeafCf = _manager.GetColumnFamily(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);

            var primaryKey = MakeMessageIdKey(result.SourceChainId, result.MessageId);
            var leafKey = MakeLeafIndexKey(result.SourceChainId, result.LeafIndex);
            var serialized = Serialize(result);

            batch.Put(primaryKey, serialized, resultsCf);
            batch.Put(leafKey, serialized, byLeafCf);

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<MessageResult?> GetByMessageIdAsync(ulong sourceChainId, ulong messageId)
        {
            var key = MakeMessageIdKey(sourceChainId, messageId);
            var data = _manager.Get(RocksDbManager.CF_MSG_RESULTS, key);
            if (data == null) return Task.FromResult<MessageResult?>(null);
            return Task.FromResult<MessageResult?>(Deserialize(data));
        }

        public Task<IReadOnlyList<MessageResult>> GetAllBySourceChainOrderedByLeafIndexAsync(ulong sourceChainId)
        {
            var results = new List<MessageResult>();
            var prefix = MakeChainPrefix(sourceChainId);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;

                var data = iterator.Value();
                if (data != null)
                {
                    var result = Deserialize(data);
                    if (result != null)
                        results.Add(result);
                }

                iterator.Next();
            }

            return Task.FromResult<IReadOnlyList<MessageResult>>(results);
        }

        public Task<IReadOnlyList<MessageResult>> GetBySourceChainAsync(ulong sourceChainId, int offset, int count)
        {
            var results = new List<MessageResult>();
            var prefix = MakeChainPrefix(sourceChainId);
            int skipped = 0;

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);
            iterator.Seek(prefix);

            while (iterator.Valid() && results.Count < count)
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;

                if (skipped < offset)
                {
                    skipped++;
                    iterator.Next();
                    continue;
                }

                var data = iterator.Value();
                if (data != null)
                {
                    var result = Deserialize(data);
                    if (result != null)
                        results.Add(result);
                }

                iterator.Next();
            }

            return Task.FromResult<IReadOnlyList<MessageResult>>(results);
        }

        public Task<IReadOnlyList<ulong>> GetSourceChainIdsAsync()
        {
            var chainIds = new HashSet<ulong>();

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (key != null && key.Length >= 8)
                {
                    var chainId = ReadUInt64BigEndian(key, 0);
                    chainIds.Add(chainId);

                    var nextPrefix = MakeChainPrefix(chainId);
                    if (!IncrementLastByte(nextPrefix))
                        break;
                    iterator.Seek(nextPrefix);
                }
                else
                {
                    iterator.Next();
                }
            }

            return Task.FromResult<IReadOnlyList<ulong>>(chainIds.OrderBy(c => c).ToList());
        }

        public Task<int> GetCountAsync(ulong sourceChainId)
        {
            int count = 0;
            var prefix = MakeChainPrefix(sourceChainId);

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_MSG_RESULTS_BY_LEAF);
            iterator.Seek(prefix);

            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (!Nethereum.Util.ByteUtil.StartsWith(key, prefix))
                    break;
                count++;
                iterator.Next();
            }

            return Task.FromResult(count);
        }

        private static byte[] MakeMessageIdKey(ulong sourceChainId, ulong messageId)
        {
            var key = new byte[16];
            WriteUInt64BigEndian(key, 0, sourceChainId);
            WriteUInt64BigEndian(key, 8, messageId);
            return key;
        }

        private static byte[] MakeLeafIndexKey(ulong sourceChainId, int leafIndex)
        {
            var key = new byte[12];
            WriteUInt64BigEndian(key, 0, sourceChainId);
            WriteInt32BigEndian(key, 8, leafIndex);
            return key;
        }

        private static byte[] MakeChainPrefix(ulong sourceChainId)
        {
            var prefix = new byte[8];
            WriteUInt64BigEndian(prefix, 0, sourceChainId);
            return prefix;
        }

        private const byte SERIALIZATION_VERSION = 1;
        private const int V0_SIZE = 85; // original: no version byte
        private const int V1_SIZE = 86; // version byte + same payload

        private static byte[] Serialize(MessageResult result)
        {
            var buffer = new byte[V1_SIZE];
            int offset = 0;

            buffer[offset] = SERIALIZATION_VERSION; offset += 1;

            WriteUInt64BigEndian(buffer, offset, result.SourceChainId); offset += 8;
            WriteUInt64BigEndian(buffer, offset, result.MessageId); offset += 8;
            WriteInt32BigEndian(buffer, offset, result.LeafIndex); offset += 4;

            CopyNormalized32(result.TxHash ?? Array.Empty<byte>(), buffer, offset); offset += 32;

            buffer[offset] = result.Success ? (byte)1 : (byte)0;
            offset += 1;

            CopyNormalized32(result.DataHash ?? Array.Empty<byte>(), buffer, offset);

            return buffer;
        }

        private static MessageResult? Deserialize(byte[] data)
        {
            if (data == null) return null;

            int offset;
            if (data.Length >= V1_SIZE && data[0] == SERIALIZATION_VERSION)
            {
                offset = 1; // skip version byte
            }
            else if (data.Length >= V0_SIZE)
            {
                offset = 0; // v0 format: no version byte
            }
            else
            {
                return null;
            }

            var sourceChainId = ReadUInt64BigEndian(data, offset); offset += 8;
            var messageId = ReadUInt64BigEndian(data, offset); offset += 8;
            var leafIndex = ReadInt32BigEndian(data, offset); offset += 4;

            var txHash = new byte[32];
            Buffer.BlockCopy(data, offset, txHash, 0, 32); offset += 32;

            var success = data[offset] != 0; offset += 1;

            var dataHash = new byte[32];
            Buffer.BlockCopy(data, offset, dataHash, 0, 32);

            return new MessageResult
            {
                SourceChainId = sourceChainId,
                MessageId = messageId,
                LeafIndex = leafIndex,
                TxHash = txHash,
                Success = success,
                DataHash = dataHash
            };
        }

        private static void CopyNormalized32(byte[] source, byte[] dest, int destOffset)
        {
            if (source == null || source.Length == 0) return;
            if (source.Length >= 32)
            {
                Buffer.BlockCopy(source, 0, dest, destOffset, 32);
            }
            else
            {
                Buffer.BlockCopy(source, 0, dest, destOffset + 32 - source.Length, source.Length);
            }
        }

        private static void WriteUInt64BigEndian(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)(value >> 56);
            buffer[offset + 1] = (byte)(value >> 48);
            buffer[offset + 2] = (byte)(value >> 40);
            buffer[offset + 3] = (byte)(value >> 32);
            buffer[offset + 4] = (byte)(value >> 24);
            buffer[offset + 5] = (byte)(value >> 16);
            buffer[offset + 6] = (byte)(value >> 8);
            buffer[offset + 7] = (byte)value;
        }

        private static ulong ReadUInt64BigEndian(byte[] buffer, int offset)
        {
            return ((ulong)buffer[offset] << 56) |
                   ((ulong)buffer[offset + 1] << 48) |
                   ((ulong)buffer[offset + 2] << 40) |
                   ((ulong)buffer[offset + 3] << 32) |
                   ((ulong)buffer[offset + 4] << 24) |
                   ((ulong)buffer[offset + 5] << 16) |
                   ((ulong)buffer[offset + 6] << 8) |
                   buffer[offset + 7];
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) |
                   (buffer[offset + 1] << 16) |
                   (buffer[offset + 2] << 8) |
                   buffer[offset + 3];
        }

        private static bool IncrementLastByte(byte[] data)
        {
            for (int i = data.Length - 1; i >= 0; i--)
            {
                if (data[i] < 0xFF) { data[i]++; return true; }
                data[i] = 0;
            }
            return false;
        }

    }
}
