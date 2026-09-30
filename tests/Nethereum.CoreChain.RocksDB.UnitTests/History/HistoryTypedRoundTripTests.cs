using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class HistoryTypedRoundTripTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"histtyped_{Guid.NewGuid():N}");
        private readonly RocksDb _db;
        private readonly RocksDbHistoryStore _store;
        private readonly IBlockEncodingProvider _provider = RlpBlockEncodingProvider.Instance;
        private readonly RocksDbSerializer _serializer = RocksDbSerializer.Default;

        public HistoryTypedRoundTripTests()
        {
            Directory.CreateDirectory(_dir);
            var cache = RocksProfiles.CreateSharedCache(32 * 1024 * 1024);
            _db = RocksDb.Open(RocksProfiles.Db(2, 64 * 1024 * 1024), Path.Combine(_dir, "db"), RocksProfiles.BuildColumnFamilies(cache));
            _store = new RocksDbHistoryStore(_db, new HistoryReorgDecoder(_provider));
        }

        public void Dispose()
        {
            _db.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public void TypedBlock_Writes_Via_Factory_And_Decodes_Back()
        {
            var pb = MakeBlock(100);
            _store.AppendBlock(HistoryBlockWriteFactory.FromPersistable(pb, _provider, _serializer));

            var header = _provider.DecodeBlockHeader(_store.GetHeader(100));
            Assert.Equal((BigInteger)100, header.BlockNumber.ToBigInteger());

            var tx0 = _provider.DecodeTransaction(_store.GetTransaction(100, 0));
            Assert.Equal(pb.Transactions[0].Hash.ToHex(), tx0.Hash.ToHex());

            var ri = _serializer.DeserializeReceiptInfoWith(_store.GetReceipt(100, 1));
            Assert.Equal((BigInteger)21000, ri.GasUsed);
            Assert.Equal((BigInteger)1_000_000_000, ri.EffectiveGasPrice);
            Assert.Equal(1, ri.TransactionIndex);
            Assert.Equal(Hash(100).ToHex(), ri.BlockHash.ToHex());

            Assert.Equal(new TxLocation(100, 0), _store.FindTransaction(pb.Transactions[0].Hash));
            var meta = BlockMetaCodec.Decode(_store.GetBlockMeta(100));
            Assert.Equal(Hash(100).ToHex(), meta.BlockHash.ToHex());
            Assert.Equal(2, meta.TxCount);
        }

        [Fact]
        public void Reorg_With_Production_Decoder_Cleans_HashIndex()
        {
            _store.AppendBlock(HistoryBlockWriteFactory.FromPersistable(MakeBlock(100), _provider, _serializer));
            var b101 = MakeBlock(101);
            _store.AppendBlock(HistoryBlockWriteFactory.FromPersistable(b101, _provider, _serializer));

            Assert.Equal(new TxLocation(101, 0), _store.FindTransaction(b101.Transactions[0].Hash));

            _store.HandleReorg(100);

            Assert.Null(_store.GetHeader(101));
            Assert.Null(_store.GetTransaction(101, 0));
            Assert.Null(_store.FindTransaction(b101.Transactions[0].Hash));
            Assert.Null(_store.FindBlock(Hash(101)));
            Assert.NotNull(_store.GetHeader(100));
        }

        private PersistableBlock MakeBlock(long n)
        {
            var txs = new List<ISignedTransaction> { MakeTx((byte)(n & 0xFF), 0), MakeTx((byte)(n & 0xFF), 1) };
            var receipts = new List<ReceiptSaveItem>();
            for (int i = 0; i < txs.Count; i++)
            {
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = new EvmUInt256((ulong)((i + 1) * 21000)), Logs = new List<Log>() };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[i].Hash, i, 21000, null, 1_000_000_000));
            }
            return new PersistableBlock(
                MakeHeader(n), Hash(n), new List<BlockHeader>(), null,
                txs, receipts, new List<(List<Log>, byte[], int)>(), new byte[256]);
        }

        private static byte[] Hash(long n) { var h = new byte[32]; h[0] = (byte)(0xB0 + n); return h; }

        private static ISignedTransaction MakeTx(byte block, byte idx)
        {
            var r = new byte[32]; r[0] = 1;
            var s = new byte[32]; s[0] = 1;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((block + 1) * 1000 + idx), gasPrice: new byte[] { 1 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: Array.Empty<byte>(), data: Array.Empty<byte>(), r: r, s: s, v: 27);
        }

        private static BlockHeader MakeHeader(long n) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)n),
            ParentHash = new byte[32],
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };
    }
}
