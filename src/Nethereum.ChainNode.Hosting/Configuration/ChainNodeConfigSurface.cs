using System;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public enum ChainNodeKind
    {
        AppChain,
        DevChain
    }

    public abstract record ChainNodeFieldLiveness
    {
        public sealed record Live(string ConsumerTypeName, string ConsumerMemberName) : ChainNodeFieldLiveness;

        public sealed record Disabled(string Reason) : ChainNodeFieldLiveness;

        public sealed record NotYetWired(string Reason) : ChainNodeFieldLiveness;
    }

    public sealed record ChainNodeConfigField(
        string Section,
        string Path,
        string TypeLiteral,
        string Description,
        string DefaultLiteral,
        bool HasFriendlyFlag,
        bool Hidden,
        Func<ChainNodeKind, ChainNodeFieldLiveness> LivenessFor)
    {
        public ChainNodeFieldLiveness LivenessOn(ChainNodeKind kind) => LivenessFor(kind);
    }

    public static class ChainNodeConfigSurface
    {
        public static readonly IReadOnlyList<ChainNodeConfigField> All = BuildAll();

        private static ChainNodeFieldLiveness Live(string consumerType, string consumerMember) =>
            new ChainNodeFieldLiveness.Live(consumerType, consumerMember);

        private static ChainNodeFieldLiveness Disabled(string reason) => new ChainNodeFieldLiveness.Disabled(reason);

        private static ChainNodeFieldLiveness NotYetWired(string reason) => new ChainNodeFieldLiveness.NotYetWired(reason);

        private static Func<ChainNodeKind, ChainNodeFieldLiveness> Both(ChainNodeFieldLiveness liveness) => _ => liveness;

        private const string ChainNodeStorage = "Nethereum.ChainNode.Hosting.ChainNodeStorage";
        private const string ChainNodeStorageConfigOpen = "Open";
        private const string ChainNodeStorageBuildOptions = "BuildStorageOptions";
        private const string ChainNode = "Nethereum.ChainNode.Hosting.ChainNode";
        private const string ChainNodeConfigType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeConfig";
        private const string ChainNodeNetworkConfigType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeNetworkConfig";
        private const string ChainNodeSyncConfigType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeSyncConfig";
        private const string ChainNodeSyncStack = "Nethereum.ChainNode.Hosting.ChainNodeSyncStack";
        private const string ChainNodeServeListener = "Nethereum.ChainNode.Hosting.ChainNodeServeListener";
        private const string ChainNodeMempool = "Nethereum.ChainNode.Hosting.ChainNodeMempool";
        private const string ChainNodeMempoolConfigType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeMempoolConfig";
        private const string ChainNodeMaintenanceRunner = "Nethereum.ChainNode.Hosting.ChainNodeMaintenanceRunner";
        private const string ChainNodeRpcConfigExtensionsType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeRpcConfigExtensions";
        private const string AppChainServerRunner = "Nethereum.AppChain.Server.AppChainServerRunner";
        private const string PrometheusExtensions = "Microsoft.Extensions.Hosting.Extensions";
        private const string ChainNodeFollowerOptionsBuilderType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeFollowerOptionsBuilder";
        private const string ChainNodeSnapBootstrapOptionsBuilderType = "Nethereum.ChainNode.Hosting.Configuration.ChainNodeSnapBootstrapOptionsBuilder";
        private const string AppChainDevP2PFollowerType = "Nethereum.AppChain.Server.Hosting.AppChainDevP2PFollower";
        private const string DevChainServerConfigType = "Nethereum.DevChain.Configuration.DevChainServerConfig";

        private static ChainNodeFieldLiveness DiscoveryDisabled() => Disabled(
            "discv4/discv5 peer discovery is not supported on this node type; it uses a curated peer mesh " +
            "(Network:TrustedPeers/TrustedBootnodes) instead. ChainNodeDiscoveryValidator.RefuseIfRequested " +
            "throws InvalidOperationException at startup if you try to enable it.");

        private static ChainNodeFieldLiveness SyncCatchUpNotYetWiredOnDevChain() => NotYetWired(
            "DevChain has no follower/catch-up mode exercised today: DevChainComposition.ComposeAsync " +
            "(DevChainComposition.cs:45-48) builds a ChainNodeSyncStack when Sync.Mode != None, but nothing " +
            "ever calls FollowerService.RunAsync or SnapSyncOrchestrator.RunAsync against it - the stack is " +
            "only bridged into the mempool for peer relay. AppChain's follower honours this field via " +
            "ChainNodeFollowerOptionsBuilder/ChainNodeSnapBootstrapOptionsBuilder (AppChainDevP2PFollower.cs).");

        private static ChainNodeFieldLiveness SnapPhaseNotYetWiredOnDevChain() => NotYetWired(
            "Same as the Sync catch-up fields: DevChain never runs a snap-bootstrap/follower loop today " +
            "(DevChainComposition.cs:45-48). AppChain's follower honours this field via " +
            "ChainNodeSnapBootstrapOptionsBuilder.Build (AppChainDevP2PFollower.cs).");

        private static List<ChainNodeConfigField> BuildAll()
        {
            var fields = new List<ChainNodeConfigField>();

            void Add(
                string section, string path, string type, string description, string defaultLiteral,
                bool hasFriendlyFlag, bool hidden, Func<ChainNodeKind, ChainNodeFieldLiveness> liveness) =>
                fields.Add(new ChainNodeConfigField(section, path, type, description, defaultLiteral, hasFriendlyFlag, hidden, liveness));

            void AddLive(string section, string path, string type, string description, string defaultLiteral,
                bool hasFriendlyFlag, string consumerType, string consumerMember) =>
                Add(section, path, type, description, defaultLiteral, hasFriendlyFlag, hidden: false,
                    Both(Live(consumerType, consumerMember)));

            Add("RPC", "Rpc.Host", "string", "RPC bind host", "127.0.0.1", hasFriendlyFlag: true, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(AppChainServerRunner, "RunAsync")
                    : Live(DevChainServerConfigType, "Host"));
            Add("RPC", "Rpc.Port", "int", "RPC bind port", "8545", hasFriendlyFlag: true, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(AppChainServerRunner, "RunAsync")
                    : Live(DevChainServerConfigType, "Port"));
            AddLive("RPC", "Rpc.MetricsPort", "int", "Expose Prometheus /metrics on this port, 0=off", "0", false,
                PrometheusExtensions, "AddPrometheusMetrics");
            AddLive("RPC", "Rpc.MaxLogBlockRange", "int", "eth_getLogs/eth_getFilterLogs max block range, 0=uncapped", "10000", false,
                ChainNodeRpcConfigExtensionsType, "ApplyTo");
            AddLive("RPC", "Rpc.MaxLogResults", "int", "eth_getLogs/eth_getFilterLogs max result count, 0=uncapped", "10000", false,
                ChainNodeRpcConfigExtensionsType, "ApplyTo");
            AddLive("RPC", "Rpc.GasCap", "long", "eth_call/estimateGas/traceCall gas cap, 0=uncapped", "50000000", false,
                ChainNodeRpcConfigExtensionsType, "ApplyTo");

            AddLive("STORAGE", "Storage.DataDirectory", "string", "Database path", "./chain-data", true, ChainNodeStorage, ChainNodeStorageConfigOpen);
            AddLive("STORAGE", "Storage.InMemory", "bool", "Use in-memory storage", "false", true, ChainNodeStorage, ChainNodeStorageConfigOpen);
            AddLive("STORAGE", "Storage.PathKeyedState", "bool", "Path-keyed (vs legacy hash-keyed) state storage", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.JournalBlocks", "int", "Value-history retention window", "128", false, ChainNodeStorage, "BuildJournalOptions");
            AddLive("STORAGE", "Storage.TrieNodeHistoryBlocks", "int", "Trie-node history retention window", "-1", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.TrieNodeHistoryIndex", "bool", "Key-major index over the node history", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.SplitHistoryStore", "bool", "Split storage into core/history physical DBs", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.HotWindowBlocks", "int", "Core self-sufficiency rolling hot-window size, in blocks", "128", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.PromotionEnabled", "bool", "Promote cold history into the hot window on demand", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.UseFreezerHistory", "bool", "Route bulk history into the append-only geth-format freezer", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.FreezerHistoryDirectory", "string", "Directory for the freezer archive", "unset", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.BackgroundFreezeIndexing", "bool", "Build the freezer index on a background thread", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.FreezerBackgroundDegreeOfParallelism", "int", "Degree of parallelism for the background freezer index", "0", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            AddLive("STORAGE", "Storage.BlockCacheSize", "long", "RocksDB shared block cache, in bytes", "1073741824", false, ChainNodeStorage, ChainNodeStorageBuildOptions);
            Add("STORAGE", "Storage.FlushCadenceBlocks", "int", "Persist state every N blocks", "1", hasFriendlyFlag: false, hidden: true,
                Both(NotYetWired(
                    "Declared on the shared ChainNodeStorageConfig, but ChainNodeStorage.BuildStorageOptions never reads it " +
                    "(RocksDbStorageOptions has no FlushCadenceBlocks field). Only mainnet's separate flat FlushCadenceBlocks " +
                    "field (MainnetChainServerConfig, consumed at MainnetNodeComposition.cs:219-225) is wired.")));
            AddLive("STORAGE", "Storage.EnableLogIndex", "bool", "Build/serve getLogs from the legacy CF_LOGS index instead of the bloom-scan L1", "false", false, ChainNodeStorage, ChainNodeStorageBuildOptions);

            AddLive("NETWORK", "Network.Serve", "bool", "Serve eth/snap over RLPx DevP2P", "true", true, ChainNode, "StartServingAsync");
            AddLive("NETWORK", "Network.ListenPort", "int", "DevP2P eth/snap serve listen port", "30303", true, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.BindAddress", "ip", "DevP2P listen bind address", "0.0.0.0", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.DialBudgetPerSecond", "int", "Outbound dial rate limit", "5", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.MaxPeersPerIPv4Subnet", "int", "Peer cap per /24 IPv4 subnet", "10", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.MaxPeersPerIPv6Subnet", "int", "Peer cap per IPv6 subnet", "10", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.NodeKeyFile", "string", "Persisted devp2p node identity key file", "unset", true, ChainNodeNetworkConfigType, "ResolveNodeKey");
            AddLive("NETWORK", "Network.NodeKeyHex", "string", "Hex-encoded devp2p node identity private key", "unset", true, ChainNodeNetworkConfigType, "ResolveNodeKey");
            AddLive("NETWORK", "Network.TrustedPeers", "string[]", "Enode URLs dialed and marked trusted", "empty", true, ChainNodeConfigType, "ResolveDialEnodes");
            AddLive("NETWORK", "Network.TrustedBootnodes", "string[]", "Bootnodes dialed at startup, distinct from --devp2p-peers", "empty", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.TrustedNodeIds", "string[]", "Node IDs trusted regardless of source enode", "empty", false, ChainNodeConfigType, "ResolveTrustedNodeIds");
            AddLive("NETWORK", "Network.TargetPeerCount", "int", "Peer pool target size", "16", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.MaxConcurrentDials", "int", "Concurrent outbound dial limit", "10", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("NETWORK", "Network.MaxInboundPeers", "int", "Inbound peer cap", "25", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.MaxInboundPerIP", "int", "Inbound peer cap per IP", "9", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.HandshakeTimeoutMs", "int", "RLPx handshake timeout", "10000", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.IdleTimeout", "timespan", "Peer idle disconnect timeout", "00:02:00", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.MirrorRemoteStatus", "bool", "Mirror the remote peer's eth/status instead of serving this node's own", "false", false, ChainNodeServeListener, "BuildListenerOptions");
            AddLive("NETWORK", "Network.ClientId", "string", "DevP2P client identifier string", "Nethereum", false, ChainNodeServeListener, "BuildListenerOptions");
            Add("NETWORK", "Network.Discovery.DisableDiscv4", "bool", "NOT SUPPORTED on this node type (curated mesh instead) — refused at startup unless true", "true", false, hidden: false, Both(DiscoveryDisabled()));
            Add("NETWORK", "Network.Discovery.Discv4Port", "int", "NOT SUPPORTED on this node type — refused at startup unless 0", "0", false, hidden: false, Both(DiscoveryDisabled()));
            Add("NETWORK", "Network.Discovery.DisableDiscv5", "bool", "NOT SUPPORTED on this node type (curated mesh instead) — refused at startup unless true", "true", false, hidden: false, Both(DiscoveryDisabled()));
            Add("NETWORK", "Network.Discovery.Discv5Port", "int", "NOT SUPPORTED on this node type — refused at startup unless 0", "0", false, hidden: false, Both(DiscoveryDisabled()));

            AddLive("SYNC", "Sync.Mode", "SyncMode", "None|ForwardExecute|Snap", "ForwardExecute", false, ChainNode, "StartSyncAsync");
            AddLive("SYNC", "Sync.FollowPeerEnode", "string", "Enode of the node to follow; syncs from it over DevP2P instead of producing blocks", "unset", true, ChainNodeConfigType, "FollowsAPeer");
            AddLive("SYNC", "Sync.TrustedPeersOnlyTip", "bool", "Only accept tip announcements from trusted peers", "false", false, ChainNodeSyncStack, "StartAsync");
            AddLive("SYNC", "Sync.EnablePushedBlocks", "bool", "Accept blocks pushed by peers, not just pulled", "false", false, ChainNodeSyncStack, "StartAsync");
            AddLive("SYNC", "Sync.FloorTargetPeerCountByDialPool", "bool", "Floor the target peer count by the configured dial pool", "false", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("SYNC", "Sync.HeaderBatchSize", "int", "Header fetch batch size", "192", false, ChainNodeSyncStack, "BuildPullSource");
            AddLive("SYNC", "Sync.BodyBatchSize", "int", "Body fetch batch size", "64", false, ChainNodeSyncStack, "BuildPullSource");
            AddLive("SYNC", "Sync.MaxInFlightPerPeer", "int", "Max in-flight requests per peer", "1", false, ChainNodeSyncStack, "BuildScheduler");
            AddLive("SYNC", "Sync.MinPeerLatestBlock", "ulong", "Ignore peers below this latest-block height", "0", false, ChainNodeSyncStack, "StartAsync");
            AddLive("SYNC", "Sync.BulkSync", "bool", "WAL-off bulk-save backfill path", "false", false, ChainNodeStorage, ChainNodeStorageConfigOpen);
            Add("SYNC", "Sync.StartBlock", "ulong", "First block to sync from", "1", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeFollowerOptionsBuilderType, "Resolve")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.Blocks", "ulong", "Number of blocks to sync", "unlimited", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeFollowerOptionsBuilderType, "Resolve")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.HeadersFrom", "ulong", "Override: start the header sweep here", "unset", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeSnapBootstrapOptionsBuilderType, "Build")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.HeadersTo", "ulong", "Floor for the header sweep override", "0", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeSnapBootstrapOptionsBuilderType, "Build")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.CheckpointEvery", "ulong", "Checkpoint cadence in blocks", "50000", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeFollowerOptionsBuilderType, "BuildFollowerOptions")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.KeepLatestCheckpoints", "int", "Checkpoints retained on disk", "5", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeFollowerOptionsBuilderType, "BuildFollowerOptions")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.ReceiptBackfill", "bool", "Re-fetch and re-validate stored receipts in the background", "false", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(AppChainDevP2PFollowerType, "LaunchReceiptBackfillScrub")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.ContinueOnMismatch", "bool", "Continue past a state-root mismatch instead of halting", "false", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeFollowerOptionsBuilderType, "BuildStrictValidationPolicy")
                    : SyncCatchUpNotYetWiredOnDevChain());
            Add("SYNC", "Sync.SnapBootstrap", "bool", "Convenience alias for Sync.Mode == Snap", "false", false, hidden: true,
                Both(Live(ChainNodeSyncConfigType, "SnapBootstrap")));
            Add("SYNC", "Sync.Snap.BackwardSkeletonPhase1", "bool", "Backward header skeleton for Phase 1", "true", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeSnapBootstrapOptionsBuilderType, "Build")
                    : SnapPhaseNotYetWiredOnDevChain());
            Add("SYNC", "Sync.Snap.Phase1Only", "bool", "Run only the Phase 1 block archive, then stop", "false", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeSnapBootstrapOptionsBuilderType, "Build")
                    : SnapPhaseNotYetWiredOnDevChain());
            Add("SYNC", "Sync.Snap.Phase1First", "bool", "Complete Phase 1 before starting Phase 2", "false", false, hidden: false,
                kind => kind == ChainNodeKind.AppChain
                    ? Live(ChainNodeSnapBootstrapOptionsBuilderType, "Build")
                    : SnapPhaseNotYetWiredOnDevChain());
            AddLive("SYNC", "Sync.Snap.AdvertiseSnap2", "bool", "Advertise snap/2 to peers", "false", false, ChainNodeSyncStack, "StartPoolAsync");
            AddLive("SYNC", "Sync.Snap.SoftResponseLimit", "int", "Soft cap on snap response payload size", "unset", false, ChainNodeServeListener, "BuildSnapHandler");

            AddLive("MAINTENANCE", "Maintenance.Verbose", "bool", "Verbose maintenance logging", "false", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");
            AddLive("MAINTENANCE", "Maintenance.WipeState", "bool", "One-shot: clear state and re-run the state sync, keeping the block archive", "false", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");
            AddLive("MAINTENANCE", "Maintenance.CompactAll", "bool", "One-shot: force a full RocksDB compaction on startup", "false", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");
            AddLive("MAINTENANCE", "Maintenance.RebuildStateFromFlat", "bool", "One-shot: rebuild stale storage-trie nodes from flat state, then verify", "false", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");
            AddLive("MAINTENANCE", "Maintenance.VerifyFlat", "bool", "One-shot: write-free flat/trie verify at the committed head, then stop", "false", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");
            AddLive("MAINTENANCE", "Maintenance.VerifyFlatSampleAccountsPerShard", "long", "With VerifyFlat: verify only the first N accounts per shard, 0=full walk", "0", false, ChainNodeMaintenanceRunner, "RunRequestedOpsAsync");

            AddLive("MEMPOOL", "Mempool.MaxPoolSize", "int", "Max pending transactions held", "5000", false, ChainNodeMempoolConfigType, "CreatePool");
            AddLive("MEMPOOL", "Mempool.MaxTxsPerSender", "int", "Max pending transactions per sender", "64", false, ChainNodeMempoolConfigType, "CreatePool");
            AddLive("MEMPOOL", "Mempool.Retention", "MempoolRetention", "Full|... transaction retention policy", "Full", false, ChainNodeMempool, "Create");
            AddLive("MEMPOOL", "Mempool.Relay", "MempoolRelay", "Ours|... transaction relay policy", "Ours", false, ChainNodeMempool, "Create");
            AddLive("MEMPOOL", "Mempool.EnableTrustedPeerAdmission", "bool", "Admit transactions relayed by trusted peers", "true", false, ChainNodeServeListener, "BuildListenerOptions");

            return fields;
        }

        public static void RenderAdvancedHelp(TextWriter writer, string nodeName, ChainNodeKind kind)
        {
            string lastSection = null;
            foreach (var field in All)
            {
                if (field.Hidden || field.HasFriendlyFlag) continue;

                if (field.Section != lastSection)
                {
                    if (lastSection != null) writer.WriteLine();
                    writer.WriteLine($"{field.Section}:");
                    lastSection = field.Section;
                }

                var liveness = field.LivenessOn(kind);
                var suffix = liveness is ChainNodeFieldLiveness.NotYetWired ? " [NOT YET WIRED]" : string.Empty;
                var canonicalKey = field.Path.Replace('.', ':');

                writer.WriteLine(
                    $"      --{nodeName}:Node:{canonicalKey} <{field.TypeLiteral}>   {field.Description}{suffix}   (default: {field.DefaultLiteral})");
            }
        }
    }
}
