using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Finality;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.Anchoring.UnitTests
{
    public class AppChainAnchoredFinalitySourceTests
    {
        private static readonly byte[] GenesisHash = Filled(0x00);
        private static readonly byte[] GenesisStateRoot = Filled(0x0f);

        [Fact]
        public async Task Given_AnAnchorAtAnUnfinalisedL1Block_When_TheCanonicalSourceIsAsked_Then_ItIsNotReported()
        {
            var blocks = await ChainAsync((5, Filled(0x05)), (9, Filled(0x09)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            anchors.At(BlockParameter.BlockParameterType.latest, Anchor(9, Filled(0x09), Filled(0x99)));

            var tip = await new AppChainAnchoredFinalitySource(anchors, blocks).GetLatestAsync(CancellationToken.None);

            Assert.Equal(5UL, tip.BlockNumber);
        }

        [Fact]
        public async Task Given_AnAnchorAtAnL1FinalisedBlock_When_TheCanonicalSourceIsAsked_Then_ItIsReported()
        {
            var blocks = await ChainAsync((5, Filled(0x05)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(5UL, tip.BlockNumber);
            Assert.Equal(Filled(0x55), tip.StateRoot);
            Assert.Equal(AnchorAcceptance.Accepted, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AnAnchorSourceReturningALowerTipThanBefore_When_Asked_Then_TheFinalisedTipDoesNotDecrease()
        {
            var blocks = await ChainAsync((3, Filled(0x03)), (5, Filled(0x05)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);
            await source.GetLatestAsync(CancellationToken.None);

            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(3, Filled(0x03), Filled(0x33)));
            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(5UL, tip.BlockNumber);
            Assert.Equal(AnchorAcceptance.NotAdvanced, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AnAnchorSourceReturningAHigherTip_When_Asked_Then_TheFinalisedTipAdvances()
        {
            var blocks = await ChainAsync((5, Filled(0x05)), (8, Filled(0x08)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);
            await source.GetLatestAsync(CancellationToken.None);

            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(8, Filled(0x08), Filled(0x88)));
            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(8UL, tip.BlockNumber);
            Assert.Equal(AnchorAcceptance.Accepted, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AnAnchorWhoseFirstBlockDescendsFromAnotherParent_When_Validated_Then_ItIsRefusedAndTheTipDoesNotAdvance()
        {
            var blocks = await ChainAsync((5, Filled(0xaa)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(0UL, tip.BlockNumber);
            Assert.Equal(AnchorAcceptance.Diverged, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AnAnchorWhoseFirstBlockDescendsFromThePreviousAnchor_When_Validated_Then_TheTipAdvances()
        {
            var blocks = await ChainAsync((5, Filled(0x05)), (8, Filled(0x08)));
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);
            await source.GetLatestAsync(CancellationToken.None);

            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(8, Filled(0x08), Filled(0x88)));
            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(8UL, tip.BlockNumber);
            Assert.Equal(Filled(0x88), tip.StateRoot);
        }

        [Fact]
        public async Task Given_TheAnchoredBlockIsNotYetInTheLocalStore_When_Validated_Then_TheTipIsUnchangedAndTheReasonIsAbsenceNotDivergence()
        {
            var blocks = await ChainAsync();
            var anchors = new FakeAnchorReader();
            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(0UL, tip.BlockNumber);
            Assert.Equal(AnchorAcceptance.NotYetLocal, source.LastAcceptance);
            Assert.NotEqual(AnchorAcceptance.Diverged, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AChainWithNoAnchorYet_When_TheSourceIsAsked_Then_TheTipIsGenesisWithGenesisHashAndStateRoot()
        {
            var blocks = await ChainAsync();
            var source = new AppChainAnchoredFinalitySource(new FakeAnchorReader(), blocks);

            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(0UL, tip.BlockNumber);
            Assert.Equal(GenesisHash, tip.BlockHash);
            Assert.Equal(GenesisStateRoot, tip.StateRoot);
            Assert.Equal(AnchorAcceptance.NoAnchorYet, source.LastAcceptance);
        }

        [Fact]
        public async Task Given_AChainWithNoAnchorYet_When_TheFirstAnchorArrives_Then_ItIsAcceptedAgainstGenesis()
        {
            var blocks = await ChainAsync((5, Filled(0x05)));
            var anchors = new FakeAnchorReader();
            var source = new AppChainAnchoredFinalitySource(anchors, blocks);
            await source.GetLatestAsync(CancellationToken.None);

            anchors.At(BlockParameter.BlockParameterType.finalized, Anchor(5, Filled(0x05), Filled(0x55)));
            var tip = await source.GetLatestAsync(CancellationToken.None);

            Assert.Equal(5UL, tip.BlockNumber);
            Assert.Equal(AnchorAcceptance.Accepted, source.LastAcceptance);
        }

        private static async Task<InMemoryBlockStore> ChainAsync(params (ulong Number, byte[] Hash)[] blocks)
        {
            var store = new InMemoryBlockStore();
            await store.SaveAsync(
                new BlockHeader { BlockNumber = 0, StateRoot = GenesisStateRoot },
                GenesisHash);

            foreach (var block in blocks)
            {
                await store.SaveAsync(
                    new BlockHeader { BlockNumber = block.Number, StateRoot = Filled((byte)(block.Number + 0x50)) },
                    block.Hash);
            }

            return store;
        }

        private static AnchorRecord Anchor(ulong endBlock, byte[] endBlockHash, byte[] postStateRoot) =>
            new AnchorRecord { EndBlock = endBlock, EndBlockHash = endBlockHash, PostStateRoot = postStateRoot };

        private static byte[] Filled(byte value)
        {
            var bytes = new byte[32];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = value;
            return bytes;
        }

        private sealed class FakeAnchorReader : IAnchorRecordReader
        {
            private readonly Dictionary<BlockParameter.BlockParameterType, AnchorRecord> _byParameter =
                new Dictionary<BlockParameter.BlockParameterType, AnchorRecord>();

            public void At(BlockParameter.BlockParameterType parameterType, AnchorRecord record) =>
                _byParameter[parameterType] = record;

            public Task<AnchorRecord> GetLatestAnchorAsync(BlockParameter blockParameter, CancellationToken ct) =>
                Task.FromResult(_byParameter.TryGetValue(blockParameter.ParameterType, out var record) ? record : null);
        }
    }
}
