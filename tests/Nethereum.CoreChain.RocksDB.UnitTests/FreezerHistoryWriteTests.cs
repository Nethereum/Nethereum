using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerHistoryWriteTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerhist_{Guid.NewGuid():N}");
        private readonly FreezerCodecSet _codecs = new FreezerCodecSet();
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private RocksDbStorageOptions FreezerOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
        };

        [Fact]
        public async Task Given_UseFreezerHistoryAndTipDeep_When_PersistAscendingBlocks_Then_ClustersAppendedAndReadBackEqual()
        {
            var blocks = MakeBlocks(0, 10);
            blocks[7] = MakeBlock(7, withWithdrawalAndUncle: true);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);
                await bundle.PersistBlocksAsync(blocks);
            }

            using var freezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(10L, freezer.Items);
            foreach (var b in blocks)
                AssertClusterRoundTrips(freezer, b);
        }

        [Fact]
        public async Task Given_MidBatchFailure_When_Persist_Then_FreezerNotWedgedAndResumable()
        {
            var initial = MakeBlocks(0, 3);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);
                await bundle.PersistBlocksAsync(initial);
            }

            long itemsBeforeFailedBatch;
            using (var baselineFreezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly))
                itemsBeforeFailedBatch = baselineFreezer.Items;
            Assert.Equal(3L, itemsBeforeFailedBatch);

            var badBatch = new List<PersistableBlock> { MakeBlock(3), MakeBlock(4), MakeBlock(6) };
            var resume = MakeBlocks(3, 3);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);

                await Assert.ThrowsAsync<FreezerConsistencyException>(() => bundle.PersistBlocksAsync(badBatch));

                await bundle.PersistBlocksAsync(resume);
            }

            using var finalFreezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(6L, finalFreezer.Items);
            for (var n = 0; n <= 5; n++)
            {
                var expected = n < 3 ? initial[n] : resume[n - 3];
                AssertClusterRoundTrips(finalFreezer, expected);
            }
        }

        [Fact]
        public async Task Given_NonContiguousDrain_When_Persist_Then_ThrowsConsistency()
        {
            var blocks = new List<PersistableBlock> { MakeBlock(0), MakeBlock(1), MakeBlock(3) };

            using var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer);
            await SetTipHeightAsync(bundle, 100_000);

            await Assert.ThrowsAsync<FreezerConsistencyException>(() => bundle.PersistBlocksAsync(blocks));
        }

        [Fact]
        public async Task Given_DrainStraddlingFreezeBoundary_When_Persist_Then_PrefixFrozenSuffixToRocksDb()
        {
            var blocks = MakeBlocks(0, 16);
            const long freezeBoundary = 10;

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 90_010);
                await bundle.PersistBlocksAsync(blocks);

                for (var n = freezeBoundary + 1; n < blocks.Count; n++)
                {
                    var b = blocks[(int)n];
                    var hashByNum = await bundle.Blocks.GetHashByNumberAsync(n);
                    Assert.Equal(b.Hash.ToHex(), hashByNum.ToHex());
                    var txs = await bundle.Transactions.GetByBlockHashAsync(b.Hash);
                    Assert.Equal(b.Transactions.Count, txs.Count);
                }
            }

            using var freezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(freezeBoundary + 1, freezer.Items);
            for (var n = 0; n <= freezeBoundary; n++)
                AssertClusterRoundTrips(freezer, blocks[(int)n]);
        }

        [Fact]
        public async Task Given_TipBelowImmutabilityThreshold_When_Persist_Then_NothingFrozenAllToRocksDb()
        {
            var blocks = MakeBlocks(0, 3);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await bundle.PersistBlocksAsync(blocks);
                foreach (var b in blocks)
                {
                    var hashByNum = await bundle.Blocks.GetHashByNumberAsync(b.Header.BlockNumber.ToBigInteger());
                    Assert.Equal(b.Hash.ToHex(), hashByNum.ToHex());
                }
            }

            using var freezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(0L, freezer.Items);
        }

        [Fact]
        public void Given_UseFreezerHistoryWithSplitHistoryStore_When_Open_Then_Rejected()
        {
            var options = FreezerOptions();
            options.SplitHistoryStore = true;

            Assert.Throws<NotSupportedException>(
                () => RocksDbChainStoreBundle.Open(DataDir, null, false, options, _signer));
        }

        [Fact]
        public async Task Given_DrainReDeliversAlreadyFrozenBlocks_When_Persist_Then_SkippedNotThrown()
        {
            var first = MakeBlocks(0, 10);
            var second = MakeBlocks(7, 8);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);

                await bundle.PersistBlocksAsync(first);
                await bundle.PersistBlocksAsync(second);
            }

            using var freezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(15L, freezer.Items);
            for (var n = 0; n <= 14; n++)
            {
                var expected = n < 10 ? first[n] : second[n - 7];
                AssertClusterRoundTrips(freezer, expected);
            }
        }

        [Fact]
        public async Task Given_DrainEntirelyBelowFreezerHead_When_Persist_Then_NoOpNoThrow()
        {
            var first = MakeBlocks(0, 10);
            var allAlreadyFrozen = MakeBlocks(2, 5);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);

                await bundle.PersistBlocksAsync(first);
                await bundle.PersistBlocksAsync(allAlreadyFrozen);
            }

            using var freezer = FreezerCore.Open(new FreezerLayout(FreezerDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(10L, freezer.Items);
        }

        [Fact]
        public async Task Given_DrainWithGapAboveHead_When_Persist_Then_StillThrowsConsistency()
        {
            var first = MakeBlocks(0, 10);
            var gapped = MakeBlocks(12, 3);

            using var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer);
            await SetTipHeightAsync(bundle, 100_000);

            await bundle.PersistBlocksAsync(first);

            await Assert.ThrowsAsync<FreezerConsistencyException>(() => bundle.PersistBlocksAsync(gapped));
        }

        [Fact]
        public async Task Given_BlockAtBalActivationWithoutBal_When_Persist_Then_Throws()
        {
            var withBal = MakeBlock(0, blockAccessListHash: Enumerable.Repeat((byte)0xAB, 32).ToArray());

            using var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer);
            await SetTipHeightAsync(bundle, 100_000);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.PersistBlocksAsync(new List<PersistableBlock> { withBal }));
        }

        [Fact]
        public async Task Given_FreezeOnlyReceiptsWithoutDerivedFields_When_Persist_Then_FrozenClusterByteIdenticalToFull()
        {
            var full = MakeBlocks(0, 5);
            var lite = full.Select(StripToFreezeOnly).ToList();

            var fullDir = Path.Combine(_root, "full");
            var liteDir = Path.Combine(_root, "lite");
            await FreezeInto(fullDir, full);
            await FreezeInto(liteDir, lite);

            using var fullFreezer = FreezerCore.Open(new FreezerLayout(fullDir), FreezerOpenMode.ReadOnly);
            using var liteFreezer = FreezerCore.Open(new FreezerLayout(liteDir), FreezerOpenMode.ReadOnly);
            Assert.Equal(5L, fullFreezer.Items);
            Assert.Equal(5L, liteFreezer.Items);
            for (long n = 0; n < 5; n++)
            {
                var f = fullFreezer.ReadCluster(n);
                var l = liteFreezer.ReadCluster(n);
                Assert.Equal(f.Header, l.Header);
                Assert.Equal(f.Hash, l.Hash);
                Assert.Equal(f.Body, l.Body);
                Assert.Equal(f.Receipts, l.Receipts);
                Assert.Equal(f.Bal, l.Bal);
            }
        }

        private async Task FreezeInto(string freezerDir, List<PersistableBlock> blocks)
        {
            var dataDir = Path.Combine(_root, "data_" + Path.GetFileName(freezerDir));
            var opts = new RocksDbStorageOptions { UseFreezerHistory = true, FreezerHistoryDirectory = freezerDir };
            using var bundle = RocksDbChainStoreBundle.Open(dataDir, null, false, opts, _signer);
            await SetTipHeightAsync(bundle, 100_000);
            await bundle.PersistBlocksAsync(blocks);
        }

        private static PersistableBlock StripToFreezeOnly(PersistableBlock full)
        {
            var stripped = new List<ReceiptSaveItem>(full.Receipts.Count);
            for (var j = 0; j < full.Receipts.Count; j++)
                stripped.Add(new ReceiptSaveItem(full.Receipts[j].Receipt, null, j, default, null, default));
            return new PersistableBlock(
                full.Header, full.Hash, full.Uncles, full.Withdrawals, full.Transactions,
                stripped, logs: null, bloom: null);
        }

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        private void AssertClusterRoundTrips(FreezerCore freezer, PersistableBlock expected)
        {
            var number = (long)expected.Header.BlockNumber.ToBigInteger();
            var cluster = freezer.ReadCluster(number);

            var header = _codecs.Headers.Decode(cluster.Header);
            Assert.Equal(expected.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());
            Assert.Equal(expected.Header.ParentHash.ToHex(), header.ParentHash.ToHex());

            Assert.Equal(expected.Hash.ToHex(), _codecs.Hashes.Decode(cluster.Hash).ToHex());

            var body = _codecs.Bodies.Decode(cluster.Body);
            Assert.Equal(expected.Transactions.Count, body.Txs.Count);

            var expectedTx = (SignedLegacyTransaction)expected.Transactions[0];
            var actualTx = (SignedLegacyTransaction)body.Txs[0];
            Assert.Equal(expectedTx.Hash.ToHex(), actualTx.Hash.ToHex());
            Assert.Equal(expectedTx.Nonce.ToHex(), actualTx.Nonce.ToHex());

            var receipts = _codecs.Receipts.Decode(cluster.Receipts);
            Assert.Equal(expected.Receipts.Count, receipts.Count);
            for (var i = 0; i < expected.Receipts.Count; i++)
            {
                var expectedReceipt = expected.Receipts[i].Receipt;
                var actualReceipt = receipts[i];
                Assert.Equal(expectedReceipt.PostStateOrStatus, actualReceipt.PostStateOrStatus);
                Assert.Equal(expectedReceipt.CumulativeGasUsed.ToBigInteger(), actualReceipt.CumulativeGasUsed);
                Assert.Equal(expectedReceipt.Logs.Count, actualReceipt.Logs.Count);

                var expectedLog = expectedReceipt.Logs[0];
                var actualLog = actualReceipt.Logs[0];
                Assert.Equal(expectedLog.Address, actualLog.Address);
                Assert.Equal(
                    expectedLog.Topics.Select(t => t.ToHex()).ToList(),
                    actualLog.Topics.Select(t => t.ToHex()).ToList());
                Assert.Equal(expectedLog.Data, actualLog.Data);
            }

            if (expected.Withdrawals != null)
            {
                Assert.NotNull(body.Withdrawals);
                Assert.Equal(expected.Withdrawals.Count, body.Withdrawals.Count);
                for (var i = 0; i < expected.Withdrawals.Count; i++)
                {
                    Assert.Equal(expected.Withdrawals[i].Index, body.Withdrawals[i].Index);
                    Assert.Equal(expected.Withdrawals[i].ValidatorIndex, body.Withdrawals[i].ValidatorIndex);
                    Assert.Equal(expected.Withdrawals[i].Address.ToHex(), body.Withdrawals[i].Address.ToHex());
                    Assert.Equal(expected.Withdrawals[i].AmountInGwei, body.Withdrawals[i].AmountInGwei);
                }
            }

            if (expected.Uncles != null && expected.Uncles.Count > 0)
            {
                Assert.Equal(expected.Uncles.Count, body.Uncles.Count);
                for (var i = 0; i < expected.Uncles.Count; i++)
                {
                    Assert.Equal(expected.Uncles[i].BlockNumber.ToBigInteger(), body.Uncles[i].BlockNumber.ToBigInteger());
                    Assert.Equal(expected.Uncles[i].ParentHash.ToHex(), body.Uncles[i].ParentHash.ToHex());
                }
            }
        }

        private static List<PersistableBlock> MakeBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private static PersistableBlock MakeBlock(long number, byte[] blockAccessListHash = null, bool withWithdrawalAndUncle = false)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);
            header.BlockAccessListHash = blockAccessListHash;

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            var receipts = new List<ReceiptSaveItem>();
            var logs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
            BigInteger cumulative = 0;
            for (var j = 0; j < txs.Count; j++)
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

            List<Withdrawal> withdrawals = null;
            var uncles = new List<BlockHeader>();
            if (withWithdrawalAndUncle)
            {
                withdrawals = new List<Withdrawal>
                {
                    new Withdrawal { Index = (ulong)number, ValidatorIndex = 7, Address = Fill(0x22, 20), AmountInGwei = 5_000_000 }
                };
                uncles.Add(MakeHeader(number > 0 ? number - 1 : 0, Fill(0x33, 32)));
            }

            return new PersistableBlock(
                header, hash,
                uncles: uncles,
                withdrawals: withdrawals,
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
            for (var i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
