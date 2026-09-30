using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.Freezer;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class ImmutabilityDepthFinalitySourceTests
    {
        private const long ImmutabilityThreshold = 90_000;

        [Fact]
        public void Given_TipAtT_When_ResolveUpperBound_Then_ItIsTipMinusImmutabilityThreshold()
        {
            var source = new ImmutabilityDepthFinalitySource(() => 100_090, ImmutabilityThreshold);

            Assert.Equal(10_090, source.FinalizedBlockNumber);
        }

        [Fact]
        public void Given_ABlockWithinTheImmutabilityWindow_When_Promote_Then_NoOp()
        {
            using var h = new Harness();
            var hot = BuildChain(10);
            var seedService = new FreezerPromotionService(h.Freezer, h.Codecs, hot, new FixedFinalitySource(5), new InMemoryRandomKeyIndexStore());
            seedService.PromoteFinalizedBlocks();
            Assert.Equal(6, h.Freezer.Items);

            var source = new ImmutabilityDepthFinalitySource(() => 8, ImmutabilityThreshold);
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, source, new InMemoryRandomKeyIndexStore());

            var result = service.PromoteFinalizedBlocks();

            Assert.Equal(0, result.PromotedCount);
            Assert.Equal(6, result.NewFreezerItems);
            Assert.Equal(6, h.Freezer.Items);
        }

        private sealed class Harness : IDisposable
        {
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }
            private readonly string _dir;

            public Harness()
            {
                _dir = Path.Combine(Path.GetTempPath(), "immutability-depth-finality-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(_dir);
                Freezer = FreezerCore.Open(new FreezerLayout(_dir), FreezerOpenMode.Append);
                Codecs = new FreezerCodecSet();
            }

            public void Dispose()
            {
                Freezer.Dispose();
                System.IO.Directory.Delete(_dir, recursive: true);
            }
        }

        private sealed class FixedFinalitySource : IFinalitySource
        {
            public FixedFinalitySource(long finalized) => FinalizedBlockNumber = finalized;
            public long FinalizedBlockNumber { get; }
        }

        private sealed class FakeHotBlockWindowSource : IHotBlockWindowSource
        {
            private readonly Dictionary<long, HotBlock> _blocks = new();
            public long HotTipNumber { get; private set; }

            public void Add(long number, HotBlock block)
            {
                _blocks[number] = block;
                if (number > HotTipNumber) HotTipNumber = number;
            }

            public HotBlock ReadHotBlock(long blockNumber) => _blocks[blockNumber];
        }

        private static byte[] BlockHashFor(long number)
        {
            var hash = new byte[32];
            hash[0] = (byte)(number + 1);
            hash[1] = (byte)(number >> 8);
            return hash;
        }

        private static BlockHeader SyntheticHeader(long blockNumber, byte[] parentHash) => new()
        {
            ParentHash = parentHash,
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

        private static HotBlock EmptyHotBlock(long number, byte[] parentHash) =>
            new(SyntheticHeader(number, parentHash), BlockHashFor(number),
                new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null),
                new List<Receipt>(), Array.Empty<byte>());

        private static FakeHotBlockWindowSource BuildChain(int count)
        {
            var hot = new FakeHotBlockWindowSource();
            var parentHash = new byte[32];
            for (var n = 0; n < count; n++)
            {
                hot.Add(n, EmptyHotBlock(n, parentHash));
                parentHash = BlockHashFor(n);
            }
            return hot;
        }
    }
}
