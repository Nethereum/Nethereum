using System;
using Nethereum.ChainNode.Hosting.Configuration;

namespace Nethereum.MainnetChain.Configuration
{
    public static class MainnetChainNodeConfigFactory
    {
        public static ChainNodeConfig ToChainNodeConfig(this MainnetChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return new ChainNodeConfig
            {
                Storage = MapStorage(config),
                Network = MapNetwork(config),
                Sync = MapSync(config),
                Rpc = MapRpc(config),
                Maintenance = MapMaintenance(config),
            };
        }

        private static ChainNodeStorageConfig MapStorage(MainnetChainServerConfig config) =>
            new ChainNodeStorageConfig
            {
                DataDirectory = config.DataDir ?? "./chain-data",
                PathKeyedState = config.PathKeyedState,
                JournalBlocks = config.JournalBlocks,
                TrieNodeHistoryBlocks = config.TrieNodeHistoryBlocks,
                TrieNodeHistoryIndex = config.TrieNodeHistoryIndex,
                SplitHistoryStore = config.SplitHistoryStore,
                HotWindowBlocks = config.HotWindowBlocks,
                PromotionEnabled = config.PromotionEnabled,
                UseFreezerHistory = config.UseFreezerHistory,
                FreezerHistoryDirectory = config.FreezerHistoryDirectory,
                BackgroundFreezeIndexing = config.BackgroundFreezeIndexing,
                FreezerBackgroundDegreeOfParallelism = config.FreezerBackgroundDegreeOfParallelism,
                BlockCacheSize = config.BlockCacheSize,
                FlushCadenceBlocks = config.FlushCadenceBlocks,
                EnableLogIndex = config.EnableLogIndex,
            };

        private static ChainNodeNetworkConfig MapNetwork(MainnetChainServerConfig config) =>
            new ChainNodeNetworkConfig
            {
                Serve = true,
                ListenPort = config.ListenPort,
                NodeKeyFile = config.NodeKeyFile,
                TrustedPeers = string.IsNullOrWhiteSpace(config.TrustedPeer)
                    ? Array.Empty<string>()
                    : config.TrustedPeer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                TargetPeerCount = config.TargetPeers,
                MirrorRemoteStatus = true,
                ClientId = "Nethereum/mainnet-server",
                Discovery = new ChainNodeDiscoveryConfig
                {
                    DisableDiscv4 = config.DisableDiscv4,
                    Discv4Port = config.Discv4Port,
                    DisableDiscv5 = config.DisableDiscv5,
                    Discv5Port = config.Discv5Port,
                },
            };

        private static ChainNodeSyncConfig MapSync(MainnetChainServerConfig config) =>
            new ChainNodeSyncConfig
            {
                Mode = config.SnapBootstrap ? SyncMode.Snap : SyncMode.ForwardExecute,
                HeaderBatchSize = config.HeadersBatch,
                BodyBatchSize = config.BodiesBatch,
                BulkSync = config.BulkSync,
                StartBlock = config.StartBlock,
                Blocks = config.Blocks,
                HeadersFrom = config.HeadersFrom,
                HeadersTo = config.HeadersTo,
                CheckpointEvery = config.CheckpointEvery,
                KeepLatestCheckpoints = config.KeepLatestCheckpoints,
                ReceiptBackfill = config.ReceiptBackfill,
                ContinueOnMismatch = config.ContinueOnMismatch,
                Snap = new ChainNodeSnapConfig
                {
                    BackwardSkeletonPhase1 = config.BackwardSkeletonPhase1,
                    Phase1Only = config.SnapPhase1Only,
                    Phase1First = config.SnapPhase1First,
                },
            };

        private static ChainNodeRpcConfig MapRpc(MainnetChainServerConfig config) =>
            new ChainNodeRpcConfig
            {
                Host = config.Host,
                Port = config.Port,
                MetricsPort = config.MetricsPort,
                MaxLogBlockRange = config.RpcMaxLogBlockRange,
                MaxLogResults = config.RpcMaxLogResults,
                GasCap = config.RpcGasCap,
            };

        private static ChainNodeMaintenanceConfig MapMaintenance(MainnetChainServerConfig config) =>
            new ChainNodeMaintenanceConfig
            {
                Verbose = config.Verbose,
                WipeState = config.WipeState,
                CompactAll = config.CompactAll,
                RebuildStateFromFlat = config.RebuildStateFromFlat,
                VerifyFlat = config.VerifyFlat,
                VerifyFlatSampleAccountsPerShard = config.VerifyFlatSampleAccountsPerShard,
            };
    }
}
