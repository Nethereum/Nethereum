using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.Freezer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer.UnitTests.FilterMaps
{
    public class SegmentChainViewTests
    {
        private const int BlockCount = 40;

        private sealed class Harness : IDisposable
        {
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }
            private readonly string _tempDir;

            public Harness(FreezerCore freezer, FreezerCodecSet codecs, string tempDir)
            {
                Freezer = freezer;
                Codecs = codecs;
                _tempDir = tempDir;
            }

            public void Dispose()
            {
                Freezer.Dispose();
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private static Harness OpenRolledStore()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "segment-chainview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var codecs = new FreezerCodecSet();
            var body = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null);
            var bal = Array.Empty<byte>();

            using (var writer = FreezerCore.Open(new FreezerLayout(tempDir, maxFileSize: 256), FreezerOpenMode.Append))
            {
                var batch = writer.BeginBatch();
                for (long b = 0; b < BlockCount; b++)
                {
                    var hash = new byte[32];
                    hash[0] = (byte)b;
                    batch.AppendCluster(b, new FrozenBlockCluster(
                        codecs.Headers.Encode(SyntheticHeader(100 + b)), hash,
                        codecs.Bodies.Encode(body), codecs.Receipts.Encode(ReceiptsFor(b)), codecs.Bals.Encode(bal)));
                }
                batch.Commit();
            }

            var freezer = FreezerCore.Open(new FreezerLayout(tempDir, maxFileSize: 256), FreezerOpenMode.ReadOnly);
            return new Harness(freezer, codecs, tempDir);
        }

        private static List<ReceiptForStorage> ReceiptsFor(long b)
        {
            var logs = new List<Log>();
            for (var i = 0; i < (int)(b % 3); i++)
            {
                var address = new byte[20];
                address[0] = (byte)b;
                address[19] = (byte)i;
                logs.Add(new Log
                {
                    Address = address.ToHex(true),
                    Topics = new List<byte[]> { TopicBytes((byte)(b + i)) },
                    Data = new byte[] { (byte)b, (byte)i },
                });
            }
            return new List<ReceiptForStorage> { new ReceiptForStorage(new byte[] { (byte)(b & 1) }, 21000 * (b + 1), logs) };
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
        public void Given_RolledStoreWithSealedAndTailBlocks_When_SegmentAndFreezerViewsCompared_Then_EveryBlockMatches()
        {
            using var h = OpenRolledStore();
            var oracle = new FreezerChainView(h.Freezer, h.Codecs);
            var segment = new SegmentChainView(h.Freezer, h.Codecs, windowSize: 8, maxDegreeOfParallelism: 4);
            var head = h.Freezer.Items - 1;

            var sealedHead = h.Freezer.SealedHead("receipts");
            Assert.True(sealedHead > 8, "need several sealed windows to exercise reloads");
            Assert.True(sealedHead <= head, "need an open tail so the per-block fallback path is exercised");

            Assert.Equal(oracle.HeadNumber, segment.HeadNumber);
            for (var n = 0L; n <= head; n++)
            {
                Assert.Equal(h.Codecs.Receipts.Encode(oracle.Receipts(n)), h.Codecs.Receipts.Encode(segment.Receipts(n)));
                Assert.Equal(h.Codecs.Headers.Encode(oracle.Header(n)), h.Codecs.Headers.Encode(segment.Header(n)));
                Assert.Equal(oracle.BlockId(n), segment.BlockId(n));
            }
        }

        [Fact]
        public void Given_NonMonotonicAccess_When_SegmentViewReQueriesEarlierBlocks_Then_StillMatchesTheOracle()
        {
            using var h = OpenRolledStore();
            var oracle = new FreezerChainView(h.Freezer, h.Codecs);
            var segment = new SegmentChainView(h.Freezer, h.Codecs, windowSize: 8, maxDegreeOfParallelism: 4);
            var head = h.Freezer.Items - 1;

            for (var n = 0L; n <= head; n++) segment.Receipts(n);

            foreach (var n in new[] { head, 2L, head - 1, 0L, 17L })
                Assert.Equal(h.Codecs.Receipts.Encode(oracle.Receipts(n)), h.Codecs.Receipts.Encode(segment.Receipts(n)));
        }
    }
}
