using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class RocksDbHistoryStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"histstore_{Guid.NewGuid():N}");
        private readonly RocksDb _db;
        private readonly RocksDbHistoryStore _store;

        public RocksDbHistoryStoreTests()
        {
            Directory.CreateDirectory(_dir);
            var cache = RocksProfiles.CreateSharedCache(32 * 1024 * 1024);
            _db = RocksDb.Open(RocksProfiles.Db(2, 64 * 1024 * 1024), _dir, RocksProfiles.BuildColumnFamilies(cache));
            _store = new RocksDbHistoryStore(_db, new HashPrefixDecoder());
        }

        public void Dispose()
        {
            _db.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public void Append_Read_RangeScan_Reorg_ReAppend()
        {
            for (ulong n = 1; n <= 5; n++) _store.AppendBlock(MakeBlock(n, txCount: 2));

            Assert.Equal("hdr3", Str(_store.GetHeader(3)));
            Assert.Equal(TxBodyBytes(3, 0), _store.GetTransaction(3, 0));
            Assert.Equal("rcpt3.1", Str(_store.GetReceipt(3, 1)));
            Assert.Equal(new TxLocation(3, 1), _store.FindTransaction(TxHash(3, 1)));
            Assert.Equal(4UL, _store.FindBlock(BlockHash(4)));

            var b2 = _store.GetBlockTransactions(2);
            Assert.Equal(2, b2.Count);
            Assert.Equal(TxBodyBytes(2, 0), b2[0]);
            Assert.Equal(TxBodyBytes(2, 1), b2[1]);

            _store.HandleReorg(3);
            Assert.Null(_store.GetHeader(4));
            Assert.Null(_store.GetHeader(5));
            Assert.Null(_store.GetTransaction(4, 0));
            Assert.Empty(_store.GetBlockTransactions(5));
            Assert.Null(_store.FindTransaction(TxHash(4, 0)));
            Assert.Null(_store.FindBlock(BlockHash(5)));

            Assert.Equal("hdr3", Str(_store.GetHeader(3)));
            Assert.Equal(new TxLocation(3, 0), _store.FindTransaction(TxHash(3, 0)));

            _store.AppendBlock(MakeBlock(4, txCount: 1, fork: true));
            Assert.Equal("hdr4-fork", Str(_store.GetHeader(4)));
            Assert.Equal(new TxLocation(4, 0), _store.FindTransaction(TxHash(4, 0, fork: true)));
            Assert.Null(_store.FindTransaction(TxHash(4, 0)));
        }

        [Fact]
        public void GetTip_Tracks_Highest_Block_Across_Append_And_Reorg()
        {
            Assert.Null(_store.GetTip());
            for (ulong n = 1; n <= 5; n++) _store.AppendBlock(MakeBlock(n, txCount: 1));
            Assert.Equal(5UL, _store.GetTip());
            _store.HandleReorg(3);
            Assert.Equal(3UL, _store.GetTip());
        }

        private static HistoryBlockWrite MakeBlock(ulong n, int txCount, bool fork = false)
        {
            var txs = new List<HistoryTxWrite>();
            for (int i = 0; i < txCount; i++)
                txs.Add(new HistoryTxWrite
                {
                    TxHash = TxHash(n, (uint)i, fork),
                    Tx = TxBodyBytes(n, (uint)i, fork),
                    Receipt = Enc($"rcpt{n}.{i}"),
                });
            return new HistoryBlockWrite
            {
                BlockNumber = n,
                BlockHash = BlockHash(n, fork),
                Header = Enc(fork ? $"hdr{n}-fork" : $"hdr{n}"),
                Meta = Concat(BlockHash(n, fork), Enc($"meta{n}")),
                Transactions = txs,
            };
        }

        private static byte[] TxBodyBytes(ulong n, uint i, bool fork = false)
            => Concat(TxHash(n, i, fork), Enc($"tx{n}.{i}"));

        private static byte[] TxHash(ulong n, uint i, bool fork = false)
            => Hash32((byte)(0x10 + n), (byte)(i + (fork ? 0x80 : 0)));

        private static byte[] BlockHash(ulong n, bool fork = false)
            => Hash32((byte)(0xB0 + n), (byte)(fork ? 0x80 : 0));

        private static byte[] Hash32(byte a, byte b)
        {
            var h = new byte[32];
            h[0] = a; h[1] = b;
            return h;
        }

        private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);
        private static string Str(byte[] b) => b == null ? null : Encoding.UTF8.GetString(b);
        private static byte[] Concat(byte[] a, byte[] b) => a.Concat(b).ToArray();

        private sealed class HashPrefixDecoder : IHistoryReorgDecoder
        {
            public byte[] BlockHash(byte[] blockMetaValue) => blockMetaValue.AsSpan(0, 32).ToArray();
            public byte[] TxHash(byte[] txBodyValue) => txBodyValue.AsSpan(0, 32).ToArray();
        }
    }
}
