using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbBatchedBlockPersisterTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"batchpersist_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task PersistBlocksAsync_WritesRowsIdenticalToPerBlockPath()
        {
            var blocks = new List<PersistableBlock>
            {
                MakeBlock(100),
                MakeBlock(101),
                MakeBlock(102),
            };

            using (var reference = RocksDbChainStoreBundle.Open(_root + "_ref"))
            {
                foreach (var b in blocks) await PersistPerBlockAsync(reference, b);
                await AssertReadsAsync(reference, blocks);
            }

            using (var batched = RocksDbChainStoreBundle.Open(_root + "_batch"))
            {
                Assert.IsAssignableFrom<IBatchedBlockPersister>(batched);
                await ((IBatchedBlockPersister)batched).PersistBlocksAsync(blocks);
                await AssertReadsAsync(batched, blocks);
            }
        }

        private static async Task PersistPerBlockAsync(RocksDbChainStoreBundle bundle, PersistableBlock b)
        {
            var blockNumber = b.Header.BlockNumber.ToBigInteger();
            await bundle.Blocks.SaveAsync(b.Header, b.Hash);
            await bundle.Uncles.SaveAsync(b.Hash, b.Uncles);
            if (b.Transactions != null)
                await bundle.Transactions.SaveManyAsync(b.Hash, blockNumber, (IReadOnlyList<ISignedTransaction>)b.Transactions);
            if (b.Receipts != null && b.Receipts.Count > 0)
                await bundle.Receipts.SaveManyAsync(b.Hash, blockNumber, b.Receipts);
            if (b.Logs != null && b.Logs.Count > 0)
                await bundle.Logs.SaveManyLogsAsync(b.Logs, b.Hash, blockNumber);
            if (b.Bloom != null)
                await bundle.Logs.SaveBlockBloomAsync(blockNumber, b.Bloom);
        }

        private static async Task AssertReadsAsync(RocksDbChainStoreBundle bundle, IReadOnlyList<PersistableBlock> blocks)
        {
            foreach (var b in blocks)
            {
                var n = b.Header.BlockNumber.ToBigInteger();

                var hashByNum = await bundle.Blocks.GetHashByNumberAsync(n);
                Assert.Equal(b.Hash.ToHex(), hashByNum.ToHex());

                var header = await bundle.Blocks.GetByHashAsync(b.Hash);
                Assert.NotNull(header);
                Assert.Equal(n, header.BlockNumber.ToBigInteger());

                var txs = await bundle.Transactions.GetByBlockHashAsync(b.Hash);
                Assert.Equal(b.Transactions.Count, txs.Count);

                var receipts = await bundle.Receipts.GetByBlockHashAsync(b.Hash);
                Assert.Equal(b.Receipts.Count, receipts.Count);
                Assert.Equal(
                    b.Receipts.Select(r => r.Receipt.CumulativeGasUsed.ToBigInteger()).OrderBy(x => x).ToList(),
                    receipts.Select(r => r.CumulativeGasUsed.ToBigInteger()).OrderBy(x => x).ToList());

                var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                var expectedLogCount = b.Logs.Sum(l => l.Logs.Count);
                Assert.Equal(expectedLogCount, logs.Count);
            }
        }

        private static PersistableBlock MakeBlock(long number)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };
            BigInteger blockNum = number;

            var receipts = new List<ReceiptSaveItem>();
            var logs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
            BigInteger cumulative = 0;
            for (int j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = "0x" + new string('a', 40), Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
                logs.Add((rcptLogs, txs[j].Hash, j));
            }

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: logs,
                bloom: Fill(0x00, 256));
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
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

        private static ISignedTransaction MakeTx(long number, int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (int i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
