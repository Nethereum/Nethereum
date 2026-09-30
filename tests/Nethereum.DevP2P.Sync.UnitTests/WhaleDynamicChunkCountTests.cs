using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class WhaleDynamicChunkCountTests
    {
        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static readonly byte[] SomeStorageRoot = Fill(0xab);
        private static readonly byte[] SomeStateRoot = Fill(0xee);

        private static byte[] Fill(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private const ulong MaxRequestSizeBytes = 512 * 1024;
        private const ulong SlotsPerChunk = 2 * (MaxRequestSizeBytes / 64);


        [Fact]
        public void EstimateRemainingSlots_LastHashZero_ReturnsNull()
        {
            Assert.Null(SnapHashRanges.EstimateRemainingSlots(100, new byte[32]));
        }

        [Fact]
        public void EstimateRemainingSlots_NullLast_ReturnsNull()
        {
            Assert.Null(SnapHashRanges.EstimateRemainingSlots(100, null));
        }

        [Fact]
        public void EstimateRemainingSlots_EstimateTooLargeForUlong_ReturnsNull()
        {
            var last = new byte[32];
            last[31] = 0x01;
            Assert.Null(SnapHashRanges.EstimateRemainingSlots(1, last));
        }

        [Fact]
        public void EstimateRemainingSlots_HalfKeyspaceCovered_ExtrapolatesToRoughlyDoubleHashCount()
        {
            var last = SnapHashRanges.BigToHash(BigInteger.One << 255);
            Assert.Equal((ulong)4, SnapHashRanges.EstimateRemainingSlots(5, last));
        }

        [Fact]
        public void EstimateRemainingSlots_SmallPrefixCovered_ExtrapolatesLargeRemaining()
        {
            var last = SnapHashRanges.BigToHash(BigInteger.One << 248);
            Assert.Equal((ulong)509999, SnapHashRanges.EstimateRemainingSlots(2000, last));
        }


        [Fact]
        public void ComputeWhaleChunkCount_FewRemainingSlots_UsesOneChunk()
        {
            var last = SnapHashRanges.BigToHash(BigInteger.One << 255);
            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                maxChunks: 16, maxRequestSizeBytes: MaxRequestSizeBytes, hashesInPage: 5, lastSlotHash: last);
            Assert.Equal(1, chunks);
        }

        [Fact]
        public void ComputeWhaleChunkCount_ModerateRemainingSlots_UsesFewerThanCeiling()
        {
            var last = SnapHashRanges.BigToHash(BigInteger.One << 248);
            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                maxChunks: 16, maxRequestSizeBytes: MaxRequestSizeBytes, hashesInPage: 78, lastSlotHash: last);
            Assert.Equal(2, chunks);
        }

        [Fact]
        public void ComputeWhaleChunkCount_ManyRemainingSlots_CapsAtMaxChunks()
        {
            var last = SnapHashRanges.BigToHash(BigInteger.One << 248);
            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                maxChunks: 16, maxRequestSizeBytes: MaxRequestSizeBytes, hashesInPage: 2000, lastSlotHash: last);
            Assert.Equal(16, chunks);
        }

        [Fact]
        public void ComputeWhaleChunkCount_LastHashZero_FallsBackToMaxChunks()
        {
            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                maxChunks: 16, maxRequestSizeBytes: MaxRequestSizeBytes, hashesInPage: 5000, lastSlotHash: new byte[32]);
            Assert.Equal(16, chunks);
        }

        [Fact]
        public void ComputeWhaleChunkCount_NullLastHash_FallsBackToMaxChunks()
        {
            var chunks = SnapHashRanges.ComputeWhaleChunkCount(
                maxChunks: 16, maxRequestSizeBytes: MaxRequestSizeBytes, hashesInPage: 5000, lastSlotHash: null);
            Assert.Equal(16, chunks);
        }


        [Fact]
        public void CreateLargeContract_FewRemainingSlots_SplitsIntoFewerSubtasksThanCeiling()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 16 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x20);
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: whale, done: true);

            var lastSlotHash = SnapHashRanges.BigToHash(BigInteger.One << 255);
            set.CreateLargeContract(0, whale, SomeStorageRoot, SomeStateRoot, pageSlotCount: 5, lastSlotHash: lastSlotHash);

            var contract = set.Tasks[0].LargeContracts[whale];
            Assert.Equal(1, contract.Subtasks.Count);
            Assert.Equal(1, contract.Pending);
            Assert.Equal(new byte[32], contract.Subtasks[0].Next, ByteArrayComparer.Current);
            Assert.Equal(SnapHashRanges.FilledHash(0xff), contract.Subtasks[^1].Last, ByteArrayComparer.Current);
        }

        [Fact]
        public void CreateLargeContract_ManyRemainingSlots_CapsAtLargeContractConcurrency()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 16 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x21);
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: whale, done: true);

            var lastSlotHash = SnapHashRanges.BigToHash(BigInteger.One << 248);
            set.CreateLargeContract(0, whale, SomeStorageRoot, SomeStateRoot, pageSlotCount: 2000, lastSlotHash: lastSlotHash);

            var contract = set.Tasks[0].LargeContracts[whale];
            Assert.Equal(16, contract.Subtasks.Count);
            Assert.Equal(16, contract.Pending);
        }

        [Fact]
        public void CreateLargeContract_ZeroLastSlotHash_FallsBackToLargeContractConcurrency()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 4 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x22);
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: whale, done: true);

            set.CreateLargeContract(0, whale, SomeStorageRoot, SomeStateRoot, pageSlotCount: 5000, lastSlotHash: new byte[32]);

            var contract = set.Tasks[0].LargeContracts[whale];
            Assert.Equal(4, contract.Subtasks.Count);
        }

        [Fact]
        public void CreateLargeContract_SmallerConfiguredBudget_ShrinksSlotsPerChunkThreshold()
        {
            var set = new SnapTaskSet(1) { LargeContractConcurrency = 16, MaxRequestSizeBytes = 640 };
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            var whale = Acc(0x23);
            set.CompleteAccountRange(frag, new List<AccountClassification>(), lastHash: whale, done: true);

            var lastSlotHash = SnapHashRanges.BigToHash(BigInteger.One << 255);
            set.CreateLargeContract(0, whale, SomeStorageRoot, SomeStateRoot, pageSlotCount: 5, lastSlotHash: lastSlotHash);

            var contract = set.Tasks[0].LargeContracts[whale];
            Assert.Equal(1, contract.Subtasks.Count);
        }
    }
}
