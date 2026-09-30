using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    public class FreezerChainViewTests
    {
        private sealed class Harness : IDisposable
        {
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }
            public FreezerChainView ChainView { get; }
            public byte[][] Hashes { get; }
            private readonly string _tempDir;

            public Harness(FreezerCore freezer, FreezerCodecSet codecs, FreezerChainView chainView,
                byte[][] hashes, string tempDir)
            {
                Freezer = freezer;
                Codecs = codecs;
                ChainView = chainView;
                Hashes = hashes;
                _tempDir = tempDir;
            }

            public void Dispose()
            {
                Freezer.Dispose();
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private static Harness OpenSyntheticStore()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "freezer-chainview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var codecs = new FreezerCodecSet();
            var hash0 = new byte[32];
            hash0[0] = 0xA0;
            var hash1 = new byte[32];
            hash1[0] = 0xB1;

            const string logAddress = "0x1111111111111111111111111111111111111111";
            var log = new Log
            {
                Address = logAddress,
                Topics = new List<byte[]> { TopicBytes(0x77) },
                Data = new byte[] { 0x01, 0x02, 0x03 },
            };
            var receiptWithLog = new ReceiptForStorage(new byte[] { 0x01 }, cumulativeGasUsed: 21000,
                new List<Log> { log });
            var block0Receipts = new List<ReceiptForStorage> { receiptWithLog };
            var block1Receipts = new List<ReceiptForStorage>();

            var body = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null);
            var bal = Array.Empty<byte>();

            using (var writer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.Append))
            {
                var batch = writer.BeginBatch();
                batch.AppendCluster(0, new FrozenBlockCluster(
                    codecs.Headers.Encode(SyntheticHeader(100)), hash0,
                    codecs.Bodies.Encode(body), codecs.Receipts.Encode(block0Receipts), codecs.Bals.Encode(bal)));
                batch.AppendCluster(1, new FrozenBlockCluster(
                    codecs.Headers.Encode(SyntheticHeader(101)), hash1,
                    codecs.Bodies.Encode(body), codecs.Receipts.Encode(block1Receipts), codecs.Bals.Encode(bal)));
                batch.Commit();
            }

            var freezer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.ReadOnly);
            var chainView = new FreezerChainView(freezer, codecs);

            return new Harness(freezer, codecs, chainView, new[] { hash0, hash1 }, tempDir);
        }

        private static byte[] TopicBytes(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static BlockHeader SyntheticHeader(long blockNumber) => new()
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            BlockNumber = new EvmUInt256((ulong)blockNumber),
            LogsBloom = new byte[256],
            Difficulty = EvmUInt256.Zero,
            Timestamp = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
        };


        [Fact]
        public void Given_FrozenBlock_When_ChainViewReceipts_Then_ReturnsFrozenReceiptLogs()
        {
            using var h = OpenSyntheticStore();

            var receipts = h.ChainView.Receipts(0);

            var receipt = Assert.Single(receipts);
            var log = Assert.Single(receipt.Logs);
            Assert.Equal("0x1111111111111111111111111111111111111111".ToLowerInvariant(),
                log.Address.ToLowerInvariant());
            Assert.Equal(TopicBytes(0x77), Assert.Single(log.Topics));
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, log.Data);
        }

        [Fact]
        public void Given_DifferentFrozenBlock_When_ChainViewReceipts_Then_ReturnsThatBlocksOwnReceipts()
        {
            using var h = OpenSyntheticStore();

            var receipts = h.ChainView.Receipts(1);

            Assert.Empty(receipts);
        }

        [Fact]
        public void Given_FrozenBlock_When_ChainViewHeaderAndBlockId_Then_MatchFrozen()
        {
            using var h = OpenSyntheticStore();

            var header = h.ChainView.Header(0);
            var blockId = h.ChainView.BlockId(0);

            Assert.Equal(100L, header.BlockNumber.ToLong());
            Assert.Equal(h.Hashes[0], blockId);
        }

        [Fact]
        public void Given_Freezer_When_HeadNumber_Then_ItemsMinusOne()
        {
            using var h = OpenSyntheticStore();

            Assert.Equal(h.Freezer.Items - 1, h.ChainView.HeadNumber);
            Assert.Equal(1L, h.ChainView.HeadNumber);
        }


        [Fact]
        public void Given_Freezer_When_FinalizedBlockNumber_Then_ItemsMinusOne()
        {
            using var h = OpenSyntheticStore();
            var finality = new FreezerHeadFinalitySource(h.Freezer);

            Assert.Equal(h.Freezer.Items - 1, finality.FinalizedBlockNumber);
            Assert.Equal(1L, finality.FinalizedBlockNumber);
        }

        [Fact]
        public void Given_EmptyFreezer_When_FinalizedBlockNumber_Then_MinusOne()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "freezer-chainview-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                using (FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.Append))
                {
                }

                using var freezer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.ReadOnly);
                var finality = new FreezerHeadFinalitySource(freezer);

                Assert.Equal(0L, freezer.Items);
                Assert.Equal(-1L, finality.FinalizedBlockNumber);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
