using System;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeConfigFactoryTests
    {
        [Theory]
        [InlineData(ChainNodePreset.InMemory)]
        [InlineData(ChainNodePreset.Pruned)]
        [InlineData(ChainNodePreset.Archive)]
        [InlineData(ChainNodePreset.SnapSync)]
        [InlineData(ChainNodePreset.SnapSyncV2)]
        public void Given_APreset_When_ItIsCreated_Then_ItsStorageCombinationIsOneTheStorageLayerAccepts(
            ChainNodePreset preset)
        {
            var storage = ChainNodeConfigFactory.Create(preset).Storage;

            ChainNodeStorage.BuildStorageOptions(storage).Validate();
        }

        [Fact]
        public void Given_TheArchivePreset_When_ItIsCreated_Then_ItKeepsEveryBlockOfHistoryAndNeverPrunes()
        {
            var config = ChainNodeConfigFactory.Archive();
            var journal = ChainNodeStorage.BuildJournalOptions(config.Storage.JournalBlocks);

            Assert.True(config.Storage.PathKeyedState);
            Assert.Equal(0, config.Storage.TrieNodeHistoryBlocks);
            Assert.False(journal.EnablePruning, "an archive node must never prune its journal");
        }

        [Fact]
        public void Given_ThePrunedPreset_When_ItIsCreated_Then_ItJournalsABoundedWindowAndPrunes()
        {
            var config = ChainNodeConfigFactory.Pruned();
            var journal = ChainNodeStorage.BuildJournalOptions(config.Storage.JournalBlocks);

            Assert.Equal(128, config.Storage.TrieNodeHistoryBlocks);
            Assert.True(journal.EnablePruning);
            Assert.Equal(128, journal.MaxHistoryBlocks);
        }

        [Fact]
        public void Given_TheInMemoryPreset_When_ItIsCreated_Then_ItIsHashKeyedWithNoNodeHistory()
        {
            var config = ChainNodeConfigFactory.InMemory();

            Assert.True(config.Storage.InMemory);
            Assert.False(config.Storage.PathKeyedState);
            Assert.Equal(-1, config.Storage.TrieNodeHistoryBlocks);
            Assert.False(config.Storage.TrieNodeHistoryIndex);
        }

        [Fact]
        public void Given_TheSnapSyncPresets_When_TheyAreCreated_Then_V2AdvertisesSnap2AndV1DoesNot()
        {
            Assert.Equal(SyncMode.Snap, ChainNodeConfigFactory.SnapSync().Sync.Mode);
            Assert.False(ChainNodeConfigFactory.SnapSync().Sync.Snap.AdvertiseSnap2);
            Assert.True(ChainNodeConfigFactory.SnapSyncV2().Sync.Snap.AdvertiseSnap2);
        }

        [Fact]
        public void Given_APreset_When_AKnobIsOverriddenAfterwards_Then_TheOverrideWinsAndTheRestStands()
        {
            var config = ChainNodeConfigFactory.SnapSyncV2();
            config.Storage.DataDirectory = "/somewhere/else";
            config.Network.ListenPort = 31000;

            Assert.Equal("/somewhere/else", config.Storage.DataDirectory);
            Assert.Equal(31000, config.Network.ListenPort);
            Assert.True(config.Sync.Snap.AdvertiseSnap2);
            Assert.True(config.Storage.PathKeyedState);
        }

        [Fact]
        public void Given_AnUnknownPreset_When_ItIsRequested_Then_ItRefusesRatherThanReturningADefault()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ChainNodeConfigFactory.Create((ChainNodePreset)999));
        }
    }
}
