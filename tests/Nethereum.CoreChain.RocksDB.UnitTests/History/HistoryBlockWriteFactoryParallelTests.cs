using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class HistoryBlockWriteFactoryParallelTests
    {
        [Fact]
        public void FromPersistable_ParallelMap_IsByteIdenticalAndOrdered()
        {
            var provider = RlpBlockEncodingProvider.Instance;
            var serializer = RocksDbSerializer.Default;
            var blocks = Enumerable.Range(0, 96).Select(MakeBlock).ToList();

            var seq = blocks
                .Select(b => HistoryBlockWriteFactory.FromPersistable(b, provider, serializer))
                .ToArray();

            for (int run = 0; run < 20; run++)
            {
                var par = new HistoryBlockWrite[blocks.Count];
                Parallel.For(0, blocks.Count, i => par[i] = HistoryBlockWriteFactory.FromPersistable(blocks[i], provider, serializer));

                for (int i = 0; i < blocks.Count; i++)
                    AssertSameWrite(seq[i], par[i]);
            }
        }

        private static void AssertSameWrite(HistoryBlockWrite a, HistoryBlockWrite b)
        {
            Assert.Equal(a.BlockNumber, b.BlockNumber);
            Assert.Equal(a.BlockHash, b.BlockHash);
            Assert.Equal(a.Header, b.Header);
            Assert.Equal(a.Meta, b.Meta);

            Assert.Equal(a.Transactions.Count, b.Transactions.Count);
            for (int i = 0; i < a.Transactions.Count; i++)
            {
                Assert.Equal(a.Transactions[i].Tx, b.Transactions[i].Tx);
                Assert.Equal(a.Transactions[i].TxHash, b.Transactions[i].TxHash);
                Assert.Equal(a.Transactions[i].Receipt, b.Transactions[i].Receipt);
            }

            AssertSameTuples(a.SequentialExtra, b.SequentialExtra);
            AssertSameTuples(a.IndexExtra, b.IndexExtra);
        }

        private static void AssertSameTuples(
            IReadOnlyList<(string Cf, byte[] Key, byte[] Value)> a, IReadOnlyList<(string Cf, byte[] Key, byte[] Value)> b)
        {
            Assert.Equal(a?.Count ?? 0, b?.Count ?? 0);
            if (a == null) return;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].Cf, b[i].Cf);
                Assert.Equal(a[i].Key, b[i].Key);
                Assert.Equal(a[i].Value, b[i].Value);
            }
        }

        private static PersistableBlock MakeBlock(int n)
        {
            var txs = new List<ISignedTransaction> { MakeTx((byte)n, 0), MakeTx((byte)n, 1) };
            var receipts = new List<ReceiptSaveItem>();
            var logsPerTx = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
            for (int i = 0; i < txs.Count; i++)
            {
                var rcpt = new Receipt
                {
                    PostStateOrStatus = new byte[] { 1 },
                    CumulativeGasUsed = new EvmUInt256((ulong)((i + 1) * 21000)),
                    Logs = new List<Log>(),
                };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[i].Hash, i, 21000, null, 1_000_000_000));

                var log = new Log
                {
                    Address = "0x" + (i + 1).ToString("x40"),
                    Data = new byte[] { (byte)n, (byte)i },
                    Topics = new List<byte[]> { new byte[32] },
                };
                logsPerTx.Add((new List<Log> { log }, txs[i].Hash, i));
            }

            return new PersistableBlock(
                MakeHeader(n), Hash(n), new List<BlockHeader>(), null,
                txs, receipts, logsPerTx, new byte[256]);
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
