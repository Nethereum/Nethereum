using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainNodeConfigMapTests
    {
        private static MainnetChainServerConfig EveryFieldSet() =>
            new MainnetChainServerConfig
            {
                Host = "10.0.0.1",
                Port = 9545,
                MetricsPort = 9100,
                DataDir = "/var/nethereum",
                TrustedPeer = "enode://ab@10.0.0.2:30303",
                NodeKeyFile = "/var/nethereum/nodekey",
                Verbose = true,
                ListenPort = 30310,
                WipeState = true,
                CompactAll = true,
                RebuildStateFromFlat = true,
                VerifyFlat = true,
                VerifyFlatSampleAccountsPerShard = 77,
                SnapBootstrap = true,
                StartBlock = 12,
                Blocks = 34,
                TargetPeers = 21,
                HeadersBatch = 256,
                BodiesBatch = 96,
                CheckpointEvery = 1234,
                KeepLatestCheckpoints = 9,
                JournalBlocks = 64,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 256,
                TrieNodeHistoryIndex = false,
                BlockCacheSize = 4096,
                FlushCadenceBlocks = 7,
                BulkSync = true,
                SplitHistoryStore = true,
                HotWindowBlocks = 512,
                EnableLogIndex = true,
                PromotionEnabled = true,
                UseFreezerHistory = true,
                FreezerHistoryDirectory = "/var/freezer",
                BackgroundFreezeIndexing = true,
                FreezerBackgroundDegreeOfParallelism = 7,
                DisableDiscv5 = true,
                Discv5Port = 30311,
                DisableDiscv4 = true,
                Discv4Port = 30312,
                ContinueOnMismatch = true,
                SnapPhase1Only = true,
                SnapPhase1First = true,
                BackwardSkeletonPhase1 = false,
                HeadersFrom = 5,
                HeadersTo = 500,
                ReceiptBackfill = true,
                RpcMaxLogBlockRange = 111,
                RpcMaxLogResults = 222,
                RpcGasCap = 333,
            };

        [Fact]
        public void Given_MainnetServerConfig_When_CopiedIntoChainNodeConfig_Then_EveryMappedFieldRoundTrips()
        {
            var source = EveryFieldSet();
            var node = source.ToChainNodeConfig();

            Assert.Equal(source.DataDir, node.Storage.DataDirectory);
            Assert.Equal(source.PathKeyedState, node.Storage.PathKeyedState);
            Assert.Equal(source.TrieNodeHistoryBlocks, node.Storage.TrieNodeHistoryBlocks);
            Assert.Equal(source.TrieNodeHistoryIndex, node.Storage.TrieNodeHistoryIndex);
            Assert.Equal(source.BlockCacheSize, node.Storage.BlockCacheSize);
            Assert.Equal(source.SplitHistoryStore, node.Storage.SplitHistoryStore);
            Assert.Equal(source.HotWindowBlocks, node.Storage.HotWindowBlocks);
            Assert.Equal(source.EnableLogIndex, node.Storage.EnableLogIndex);
            Assert.Equal(source.PromotionEnabled, node.Storage.PromotionEnabled);
            Assert.Equal(source.UseFreezerHistory, node.Storage.UseFreezerHistory);
            Assert.Equal(source.FreezerHistoryDirectory, node.Storage.FreezerHistoryDirectory);
            Assert.Equal(source.BackgroundFreezeIndexing, node.Storage.BackgroundFreezeIndexing);
            Assert.Equal(source.FreezerBackgroundDegreeOfParallelism, node.Storage.FreezerBackgroundDegreeOfParallelism);
            Assert.Equal(source.FlushCadenceBlocks, node.Storage.FlushCadenceBlocks);

            Assert.Equal(source.ListenPort, node.Network.ListenPort);
            Assert.Equal(new[] { source.TrustedPeer }, node.Network.TrustedPeers);
            Assert.Equal(source.NodeKeyFile, node.Network.NodeKeyFile);
            Assert.Equal(source.TargetPeers, node.Network.TargetPeerCount);
            Assert.Equal(source.DisableDiscv4, node.Network.Discovery.DisableDiscv4);
            Assert.Equal(source.Discv4Port, node.Network.Discovery.Discv4Port);
            Assert.Equal(source.DisableDiscv5, node.Network.Discovery.DisableDiscv5);
            Assert.Equal(source.Discv5Port, node.Network.Discovery.Discv5Port);

            Assert.Equal(source.HeadersBatch, node.Sync.HeaderBatchSize);
            Assert.Equal(source.BodiesBatch, node.Sync.BodyBatchSize);
            Assert.Equal(source.StartBlock, node.Sync.StartBlock);
            Assert.Equal(source.Blocks, node.Sync.Blocks);
            Assert.Equal(source.CheckpointEvery, node.Sync.CheckpointEvery);
            Assert.Equal(source.KeepLatestCheckpoints, node.Sync.KeepLatestCheckpoints);
            Assert.Equal(source.HeadersFrom, node.Sync.HeadersFrom);
            Assert.Equal(source.HeadersTo, node.Sync.HeadersTo);
            Assert.Equal(source.ReceiptBackfill, node.Sync.ReceiptBackfill);
            Assert.Equal(source.ContinueOnMismatch, node.Sync.ContinueOnMismatch);
            Assert.Equal(source.SnapBootstrap, node.Sync.SnapBootstrap);

            Assert.Equal(source.RpcMaxLogBlockRange, node.Rpc.MaxLogBlockRange);
            Assert.Equal(source.RpcMaxLogResults, node.Rpc.MaxLogResults);
            Assert.Equal(source.RpcGasCap, node.Rpc.GasCap);
        }

        [Fact]
        public void Given_TheMainnetServerConfigWithEveryFieldSet_When_ItIsMappedToChainNodeConfig_Then_NoStorageValueIsDropped()
        {
            var node = EveryFieldSet().ToChainNodeConfig();

            Assert.Equal("/var/nethereum", node.Storage.DataDirectory);
            Assert.True(node.Storage.PathKeyedState);
            Assert.Equal(64, node.Storage.JournalBlocks);
            Assert.Equal(256, node.Storage.TrieNodeHistoryBlocks);
            Assert.False(node.Storage.TrieNodeHistoryIndex);
            Assert.True(node.Storage.SplitHistoryStore);
            Assert.Equal(512, node.Storage.HotWindowBlocks);
            Assert.True(node.Storage.PromotionEnabled);
            Assert.True(node.Storage.UseFreezerHistory);
            Assert.Equal("/var/freezer", node.Storage.FreezerHistoryDirectory);
            Assert.True(node.Storage.BackgroundFreezeIndexing);
            Assert.Equal(7, node.Storage.FreezerBackgroundDegreeOfParallelism);
            Assert.Equal(4096, node.Storage.BlockCacheSize);
            Assert.Equal(7, node.Storage.FlushCadenceBlocks);
            Assert.True(node.Storage.EnableLogIndex);
        }

        [Fact]
        public void Given_TheMainnetServerConfigWithEveryFieldSet_When_ItIsMappedToChainNodeConfig_Then_NoNetworkValueIsDropped()
        {
            var node = EveryFieldSet().ToChainNodeConfig();

            Assert.True(node.Network.Serve);
            Assert.Equal(30310, node.Network.ListenPort);
            Assert.Equal("/var/nethereum/nodekey", node.Network.NodeKeyFile);
            Assert.Equal(new[] { "enode://ab@10.0.0.2:30303" }, node.Network.TrustedPeers);
            Assert.Equal(21, node.Network.TargetPeerCount);
            Assert.True(node.Network.MirrorRemoteStatus);
            Assert.True(node.Network.Discovery.DisableDiscv4);
            Assert.Equal(30312, node.Network.Discovery.Discv4Port);
            Assert.True(node.Network.Discovery.DisableDiscv5);
            Assert.Equal(30311, node.Network.Discovery.Discv5Port);
        }

        [Fact]
        public void Given_TheMainnetServerConfigWithEveryFieldSet_When_ItIsMappedToChainNodeConfig_Then_NoSyncValueIsDropped()
        {
            var node = EveryFieldSet().ToChainNodeConfig();

            Assert.Equal(SyncMode.Snap, node.Sync.Mode);
            Assert.Equal(256, node.Sync.HeaderBatchSize);
            Assert.Equal(96, node.Sync.BodyBatchSize);
            Assert.True(node.Sync.BulkSync);
            Assert.Equal(12UL, node.Sync.StartBlock);
            Assert.Equal(34UL, node.Sync.Blocks);
            Assert.Equal(5UL, node.Sync.HeadersFrom);
            Assert.Equal(500UL, node.Sync.HeadersTo);
            Assert.Equal(1234UL, node.Sync.CheckpointEvery);
            Assert.Equal(9, node.Sync.KeepLatestCheckpoints);
            Assert.True(node.Sync.ReceiptBackfill);
            Assert.True(node.Sync.ContinueOnMismatch);
            Assert.False(node.Sync.Snap.BackwardSkeletonPhase1);
            Assert.True(node.Sync.Snap.Phase1Only);
            Assert.True(node.Sync.Snap.Phase1First);
        }

        [Fact]
        public void Given_TheMainnetServerConfigWithEveryFieldSet_When_ItIsMappedToChainNodeConfig_Then_NoRpcOrMaintenanceValueIsDropped()
        {
            var node = EveryFieldSet().ToChainNodeConfig();

            Assert.Equal("10.0.0.1", node.Rpc.Host);
            Assert.Equal(9545, node.Rpc.Port);
            Assert.Equal(9100, node.Rpc.MetricsPort);
            Assert.Equal(111, node.Rpc.MaxLogBlockRange);
            Assert.Equal(222, node.Rpc.MaxLogResults);
            Assert.Equal(333, node.Rpc.GasCap);

            Assert.True(node.Maintenance.Verbose);
            Assert.True(node.Maintenance.WipeState);
            Assert.True(node.Maintenance.CompactAll);
            Assert.True(node.Maintenance.RebuildStateFromFlat);
            Assert.True(node.Maintenance.VerifyFlat);
            Assert.Equal(77, node.Maintenance.VerifyFlatSampleAccountsPerShard);
        }

        [Fact]
        public void Given_MainnetNotSnapBootstrapping_When_Mapped_Then_TheModeIsForwardExecuteNotSnap()
        {
            var config = EveryFieldSet();
            config.SnapBootstrap = false;

            Assert.Equal(SyncMode.ForwardExecute, config.ToChainNodeConfig().Sync.Mode);
        }

        [Fact]
        public void Given_MainnetWithNoTrustedPeer_When_Mapped_Then_ThePeerSetIsEmptyNotANullEntry()
        {
            var config = EveryFieldSet();
            config.TrustedPeer = null;

            Assert.Empty(config.ToChainNodeConfig().Network.TrustedPeers);
        }

        [Fact]
        public void Given_MainnetsNodeKeyIsAFilePath_When_TheSharedConfigResolvesIt_Then_ItUsesThatPathNotAHexKey()
        {
            var node = EveryFieldSet().ToChainNodeConfig();

            Assert.Equal("/var/nethereum/nodekey", node.Network.NodeKeyFile);
            Assert.Null(node.Network.NodeKeyHex);
        }
    }
}
