using System.Collections.Generic;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.Parity
{
    public class EnginePayloadStashBoundsTests
    {
        private static byte[] HashOf(int i) => new byte[] { (byte)(i >> 8), (byte)i, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };

        private static BlockBundle BundleOf(int i, int dataBytes)
        {
            var tx = new Transaction1559(
                (EvmUInt256)1, EvmUInt256.Zero, EvmUInt256.Zero, EvmUInt256.Zero, (EvmUInt256)30_000_000,
                "0x0000000000000000000000000000000000000001", EvmUInt256.Zero, "0x" + new string('a', dataBytes * 2), new List<AccessListItem>());
            return new BlockBundle(
                new BlockHeader { BlockNumber = i + 1, ParentHash = new byte[32] },
                new List<ISignedTransaction> { tx }, new List<BlockHeader>(), new List<Withdrawal>(), HashOf(i));
        }

        [Fact]
        [Trait("Finding", "P3")]
        public void Given_PayloadsWhoseBytesExceedTheBudgetBeforeTheCountDoes_When_TheyAreStashed_Then_TheOldestAreEvictedFirstAndTheBudgetHolds()
        {
            var stash = new EnginePayloadStash(new EnginePayloadStashOptions { MaxBlocks = 1000, MaxBytes = 4_000_000 });

            for (var i = 0; i < 20; i++) stash.Put(BundleOf(i, 100_000));

            Assert.True(stash.RetainedBytes <= 4_000_000);
            Assert.True(stash.Count < 20);
            Assert.False(stash.Contains(HashOf(0)));
            Assert.True(stash.Contains(HashOf(19)));
        }

        [Fact]
        [Trait("Finding", "P3")]
        public void Given_TheBlockCountBound_When_MorePayloadsAreStashedThanItAllows_Then_TheOldestAreEvicted()
        {
            var stash = new EnginePayloadStash(new EnginePayloadStashOptions { MaxBlocks = 3 });

            for (var i = 0; i < 5; i++) stash.Put(BundleOf(i, 10));

            Assert.Equal(3, stash.Count);
            Assert.False(stash.Contains(HashOf(0)));
            Assert.False(stash.Contains(HashOf(1)));
            Assert.True(stash.Contains(HashOf(4)));
        }

        [Fact]
        [Trait("Finding", "P3")]
        public void Given_StashedPayloadsThatAreRemovedAndPruned_When_TheyLeave_Then_TheRetainedBytesFallBackToZero()
        {
            var stash = new EnginePayloadStash();
            for (var i = 0; i < 6; i++) stash.Put(BundleOf(i, 1000));

            stash.Remove(HashOf(0));
            stash.PruneBelow(10);

            Assert.Equal(0, stash.Count);
            Assert.Equal(0, stash.RetainedBytes);
        }

        [Fact]
        [Trait("Finding", "P3")]
        public void Given_APayloadRemovedAndStashedAgain_When_TheCountBoundIsReached_Then_NoLivePayloadIsEvictedEarly()
        {
            var stash = new EnginePayloadStash(new EnginePayloadStashOptions { MaxBlocks = 3 });
            stash.Put(BundleOf(0, 10));
            stash.Put(BundleOf(1, 10));
            stash.Remove(HashOf(0));
            stash.Put(BundleOf(0, 10));
            stash.Put(BundleOf(2, 10));

            Assert.Equal(3, stash.Count);
            Assert.True(stash.Contains(HashOf(1)));
        }
    }
}
