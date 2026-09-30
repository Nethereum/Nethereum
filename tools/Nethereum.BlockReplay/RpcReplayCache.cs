using System;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// Persistent KV cache of historical chain reads keyed by
    /// (parentBlockNumber, address, [slot]). All entries are immutable —
    /// the value of any quantity at any post-N block can never change,
    /// so this cache never needs an invalidation strategy. Re-running
    /// the same block (or a neighbouring block sharing accounts/slots)
    /// short-circuits the corresponding RPC fetch.
    /// </summary>
    public sealed class RpcReplayCache : IDisposable
    {
        private const byte TYPE_BALANCE  = 0x01;
        private const byte TYPE_NONCE    = 0x02;
        private const byte TYPE_CODE     = 0x03;
        private const byte TYPE_STORAGE  = 0x04;
        private const byte TYPE_BLKHASH  = 0x05;

        private readonly RocksDb _db;
        private bool _disposed;

        public long Hits { get; private set; }
        public long Misses { get; private set; }

        public RpcReplayCache(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            System.IO.Directory.CreateDirectory(path);
            var opts = new DbOptions().SetCreateIfMissing(true).EnableStatistics();
            _db = RocksDb.Open(opts, path);
        }

        public bool TryGetBalance(long parentBlock, string address, out byte[] value)
        {
            value = _db.Get(BuildAccountKey(TYPE_BALANCE, parentBlock, address));
            if (value != null) { Hits++; return true; }
            Misses++; return false;
        }
        public void PutBalance(long parentBlock, string address, byte[] value)
            => _db.Put(BuildAccountKey(TYPE_BALANCE, parentBlock, address), value);

        public bool TryGetNonce(long parentBlock, string address, out byte[] value)
        {
            value = _db.Get(BuildAccountKey(TYPE_NONCE, parentBlock, address));
            if (value != null) { Hits++; return true; }
            Misses++; return false;
        }
        public void PutNonce(long parentBlock, string address, byte[] value)
            => _db.Put(BuildAccountKey(TYPE_NONCE, parentBlock, address), value);

        public bool TryGetCode(long parentBlock, string address, out byte[] value)
        {
            value = _db.Get(BuildAccountKey(TYPE_CODE, parentBlock, address));
            if (value != null) { Hits++; return true; }
            Misses++; return false;
        }
        public void PutCode(long parentBlock, string address, byte[] value)
            => _db.Put(BuildAccountKey(TYPE_CODE, parentBlock, address), value ?? Array.Empty<byte>());

        public bool TryGetStorage(long parentBlock, string address, byte[] slotBE32, out byte[] value)
        {
            value = _db.Get(BuildStorageKey(parentBlock, address, slotBE32));
            if (value != null) { Hits++; return true; }
            Misses++; return false;
        }
        public void PutStorage(long parentBlock, string address, byte[] slotBE32, byte[] value)
            => _db.Put(BuildStorageKey(parentBlock, address, slotBE32), value ?? Array.Empty<byte>());

        public bool TryGetBlockHash(long blockNumber, out byte[] value)
        {
            var key = new byte[1 + 8];
            key[0] = TYPE_BLKHASH;
            WriteBE64(key, 1, blockNumber);
            value = _db.Get(key);
            if (value != null) { Hits++; return true; }
            Misses++; return false;
        }
        public void PutBlockHash(long blockNumber, byte[] value)
        {
            var key = new byte[1 + 8];
            key[0] = TYPE_BLKHASH;
            WriteBE64(key, 1, blockNumber);
            _db.Put(key, value);
        }

        private static byte[] BuildAccountKey(byte type, long parentBlock, string address)
        {
            var addrBytes = address.HexToByteArray();
            var key = new byte[1 + 8 + 20];
            key[0] = type;
            WriteBE64(key, 1, parentBlock);
            Buffer.BlockCopy(addrBytes, 0, key, 9, 20);
            return key;
        }

        private static byte[] BuildStorageKey(long parentBlock, string address, byte[] slotBE32)
        {
            var addrBytes = address.HexToByteArray();
            var key = new byte[1 + 8 + 20 + 32];
            key[0] = TYPE_STORAGE;
            WriteBE64(key, 1, parentBlock);
            Buffer.BlockCopy(addrBytes, 0, key, 9, 20);
            Buffer.BlockCopy(slotBE32, 0, key, 29, 32);
            return key;
        }

        private static void WriteBE64(byte[] dst, int offset, long value)
        {
            for (int i = 7; i >= 0; i--)
            {
                dst[offset + i] = (byte)(value & 0xff);
                value >>= 8;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _db?.Dispose();
        }
    }
}
