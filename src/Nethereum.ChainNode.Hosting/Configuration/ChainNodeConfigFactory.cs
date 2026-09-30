using System;
using Nethereum.DevP2P.Sync.Mempool;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public enum ChainNodePreset
    {
        InMemory,
        Pruned,
        Archive,
        SnapSync,
        SnapSyncV2,
    }

    public enum ChainNodeRole
    {
        Signer,
        Follower,
        GossipNode
    }

    public static class ChainNodeConfigFactory
    {
        public static ChainNodeConfig AsRole(this ChainNodeConfig config, ChainNodeRole role)
        {
            switch (role)
            {
                case ChainNodeRole.Signer:
                    config.Mempool.Retention = MempoolRetention.Full;
                    config.Mempool.Relay = MempoolRelay.Ours;
                    break;
                case ChainNodeRole.Follower:
                    config.Mempool.Retention = MempoolRetention.RelayOnly;
                    config.Mempool.Relay = MempoolRelay.Ours;
                    break;
                case ChainNodeRole.GossipNode:
                    config.Mempool.Retention = MempoolRetention.Full;
                    config.Mempool.Relay = MempoolRelay.All;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown chain node role.");
            }
            return config;
        }

        public static ChainNodeConfig Create(ChainNodePreset preset)
        {
            switch (preset)
            {
                case ChainNodePreset.InMemory: return InMemory();
                case ChainNodePreset.Pruned: return Pruned();
                case ChainNodePreset.Archive: return Archive();
                case ChainNodePreset.SnapSync: return SnapSync();
                case ChainNodePreset.SnapSyncV2: return SnapSyncV2();
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown chain node preset.");
            }
        }

        public static ChainNodeConfig InMemory()
        {
            var config = new ChainNodeConfig();
            config.Storage.InMemory = true;
            HashKeyed(config.Storage);
            config.Storage.JournalBlocks = -1;
            config.Sync.Mode = SyncMode.ForwardExecute;
            return config;
        }

        public static ChainNodeConfig Pruned()
        {
            var config = new ChainNodeConfig();
            PathKeyed(config.Storage, historyBlocks: 128);
            config.Storage.JournalBlocks = 128;
            config.Sync.Mode = SyncMode.ForwardExecute;
            return config;
        }

        public static ChainNodeConfig Archive()
        {
            var config = new ChainNodeConfig();
            PathKeyed(config.Storage, historyBlocks: 0);
            config.Storage.JournalBlocks = 0;
            config.Sync.Mode = SyncMode.ForwardExecute;
            return config;
        }

        public static ChainNodeConfig SnapSync()
        {
            var config = Pruned();
            config.Sync.Mode = SyncMode.Snap;
            return config;
        }

        public static ChainNodeConfig SnapSyncV2()
        {
            var config = SnapSync();
            config.Sync.Snap.AdvertiseSnap2 = true;
            return config;
        }

        private static void PathKeyed(ChainNodeStorageConfig storage, int historyBlocks)
        {
            storage.PathKeyedState = true;
            storage.TrieNodeHistoryBlocks = historyBlocks;
            storage.TrieNodeHistoryIndex = true;
        }

        private static void HashKeyed(ChainNodeStorageConfig storage)
        {
            storage.PathKeyedState = false;
            storage.TrieNodeHistoryBlocks = -1;
            storage.TrieNodeHistoryIndex = false;
        }
    }
}
