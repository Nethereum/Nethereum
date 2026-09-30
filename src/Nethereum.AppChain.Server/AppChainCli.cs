using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.Extensions.Configuration;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AppChain.Server
{
    public sealed class AppChainCliOptions
    {
        public Option<string> Host { get; } = new Option<string>("--host", () => "127.0.0.1", "Host to bind to");
        public Option<int> Port { get; } = new Option<int>("--port", () => 8546, "Port to listen on");
        public Option<long> ChainId { get; } = new Option<long>("--chain-id", () => 420420, "Chain ID");
        public Option<string> ChainName { get; } = new Option<string>("--name", () => "AppChain", "Chain name");
        public Option<string?> GenesisOwnerKey { get; } = new Option<string?>("--genesis-owner-key", "Genesis owner private key (deploys MUD, owns root namespace)");
        public Option<string?> GenesisOwnerAddress { get; } = new Option<string?>("--genesis-owner-address", "Genesis owner address (for follower mode, no private key needed)");
        public Option<string?> SequencerKey { get; } = new Option<string?>("--sequencer-key", "Sequencer private key (produces blocks)");
        public Option<string?> SequencerAddress { get; } = new Option<string?>("--sequencer-address", "Sequencer address (for follower mode, no private key needed)");
        public Option<int> BlockTime { get; } = new Option<int>("--block-time", () => 1000, "Block time in milliseconds");
        public Option<bool> AllowEmptyBlocks { get; } = new Option<bool>("--allow-empty-blocks", () => false, "Produce blocks even when no pending transactions");
        public Option<bool> DeployMud { get; } = new Option<bool>("--deploy-mud-world", () => true, "Deploy MUD World contracts at genesis (--deploy-mud-world false to skip)");
        public Option<string?> WorldSalt { get; } = new Option<string?>("--world-salt", "MUD World salt (32 bytes hex)");
        public Option<string> DbPath { get; } = new Option<string>(new[] { "--db-path", "--data-dir" }, () => "./appchain-data", "Database path");
        public Option<bool> InMemory { get; } = new Option<bool>("--in-memory", () => false, "Use in-memory storage");
        public Option<string?> L1Rpc { get; } = new Option<string?>("--l1-rpc", "L1 RPC URL for anchoring");
        public Option<string?> AnchorContract { get; } = new Option<string?>("--anchor-contract", "Anchor contract address on L1");
        public Option<string> ConsensusMode { get; } = new Option<string>("--consensus", () => "single-sequencer", "Consensus mode: single-sequencer or clique");
        public Option<bool> EnableDevP2PServe { get; } = new Option<bool>("--enable-devp2p-serve", () => false, "Serve eth/snap over RLPx DevP2P (lets snap-sync followers cold-sync from this node)");
        public Option<int> DevP2PServePort { get; } = new Option<int>(new[] { "--devp2p-serve-port", "--listen-port" }, () => 30403, "DevP2P eth/snap serve listen port");
        public Option<string[]> DevP2PPeers { get; } = new Option<string[]>(new[] { "--devp2p-peers", "--trusted-peer" }, Array.Empty<string>, "Peer enodes to dial and trust (replaces --sync-peers and --bootstrap-nodes)");
        public Option<string?> FollowPeer { get; } = new Option<string?>("--follow-peer", "Enode of the node to follow; syncs from it over DevP2P instead of producing blocks");
        public Option<string?> SignerKey { get; } = new Option<string?>("--signer-key", "Clique signer private key");
        public Option<string[]> InitialSigners { get; } = new Option<string[]>("--initial-signers", Array.Empty<string>, "Initial Clique signers (addresses)");
        public Option<int> CliquePeriod { get; } = new Option<int>("--clique-period", () => 15, "Clique block period in seconds");
        public Option<int> CliqueEpoch { get; } = new Option<int>("--clique-epoch", () => 30000, "Clique epoch length");
        public Option<bool> EnableMessaging { get; } = new Option<bool>("--enable-messaging", () => false, "Enable cross-chain message processing");
        public Option<string[]> HubSourceChains { get; } = new Option<string[]>("--hub-source-chains", Array.Empty<string>, "Hub source chains (format: chainId:rpcUrl:hubAddress)");
        public Option<int> MessagePollInterval { get; } = new Option<int>("--message-poll-interval", () => 5000, "Message poll interval in milliseconds");
        public Option<int> MaxMessagesPerPoll { get; } = new Option<int>("--max-messages-per-poll", () => 100, "Max messages per poll cycle");
        public Option<bool> EnableAcknowledgment { get; } = new Option<bool>("--enable-acknowledgment", () => false, "Enable message acknowledgment back to source Hubs");
        public Option<int> AcknowledgmentInterval { get; } = new Option<int>("--acknowledgment-interval", () => 30000, "Message acknowledgment interval in milliseconds");
        public Option<string?> OtlpEndpoint { get; } = new Option<string?>("--otlp-endpoint", "OpenTelemetry OTLP endpoint URL (falls back to OTEL_EXPORTER_OTLP_ENDPOINT env var)");
        public Option<string?> NodeKeyHex { get; } = new Option<string?>("--node-key-hex", "Hex-encoded devp2p node identity private key (pins a stable enode across restarts)");
        public Option<string?> NodeKeyFile { get; } = new Option<string?>("--node-key-file", "Persisted devp2p node identity key file (default: <db-path>/nodekey, created on first run)");

        public IEnumerable<Option> All()
        {
            yield return Host;
            yield return Port;
            yield return ChainId;
            yield return ChainName;
            yield return GenesisOwnerKey;
            yield return GenesisOwnerAddress;
            yield return SequencerKey;
            yield return SequencerAddress;
            yield return BlockTime;
            yield return AllowEmptyBlocks;
            yield return DeployMud;
            yield return WorldSalt;
            yield return DbPath;
            yield return InMemory;
            yield return L1Rpc;
            yield return AnchorContract;
            yield return ConsensusMode;
            yield return EnableDevP2PServe;
            yield return DevP2PServePort;
            yield return DevP2PPeers;
            yield return FollowPeer;
            yield return SignerKey;
            yield return InitialSigners;
            yield return CliquePeriod;
            yield return CliqueEpoch;
            yield return EnableMessaging;
            yield return HubSourceChains;
            yield return MessagePollInterval;
            yield return MaxMessagesPerPoll;
            yield return EnableAcknowledgment;
            yield return AcknowledgmentInterval;
            yield return OtlpEndpoint;
            yield return NodeKeyHex;
            yield return NodeKeyFile;
        }
    }

    public static class AppChainCli
    {
        public const string ConfigSectionName = "AppChain";

        public static (RootCommand Root, AppChainCliOptions Options) CreateRootCommand()
        {
            var options = new AppChainCliOptions();
            var root = new RootCommand("Nethereum AppChain Server - HTTP JSON-RPC server with MUD World deployment");
            foreach (var option in options.All())
                root.AddOption(option);
            root.TreatUnmatchedTokensAsErrors = false;
            return (root, options);
        }

        public static IConfiguration BuildLayeredConfiguration(string[] args) =>
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

        public static AppChainServerConfig BuildConfig(ParseResult parseResult, AppChainCliOptions options, IConfiguration layeredConfiguration)
        {
            var config = new AppChainServerConfig();
            layeredConfiguration.GetSection(ConfigSectionName).Bind(config);

            void Apply<T>(Option<T> option, Action<T> assign)
            {
                if (parseResult.FindResultFor(option) is OptionResult result && !result.IsImplicit)
                    assign(parseResult.GetValueForOption(option)!);
            }

            Apply(options.Host, v => config.Node.Rpc.Host = v);
            Apply(options.Port, v => config.Node.Rpc.Port = v);
            Apply(options.ChainId, v => config.ChainId = v);
            Apply(options.ChainName, v => config.ChainName = v);
            Apply(options.OtlpEndpoint, v => config.OtlpEndpoint = v);

            Apply(options.DbPath, v => config.Node.Storage.DataDirectory = v);
            Apply(options.InMemory, v => config.Node.Storage.InMemory = v);

            Apply(options.EnableDevP2PServe, v => config.Node.Network.Serve = v);
            Apply(options.DevP2PServePort, v => config.Node.Network.ListenPort = v);
            Apply(options.DevP2PPeers, v => config.Node.Network.TrustedPeers = v ?? Array.Empty<string>());
            Apply(options.NodeKeyHex, v => config.Node.Network.NodeKeyHex = v);
            Apply(options.NodeKeyFile, v => config.Node.Network.NodeKeyFile = v);

            Apply(options.FollowPeer, v => config.Node.Sync.FollowPeerEnode = v);

            Apply(options.GenesisOwnerKey, v => config.Genesis.Owner.PrivateKey = v);
            Apply(options.GenesisOwnerAddress, v => config.Genesis.Owner.Address = v);

            Apply(options.ConsensusMode, v => config.Consensus.Mode = AppChainConsensusModeParser.Parse(v));
            Apply(options.BlockTime, v => config.Consensus.BlockTimeMs = v);
            Apply(options.AllowEmptyBlocks, v => config.Consensus.AllowEmptyBlocks = v);
            Apply(options.SequencerKey, v => config.Consensus.Sequencer.PrivateKey = v);
            Apply(options.SequencerAddress, v => config.Consensus.Sequencer.Address = v);
            Apply(options.SignerKey, v => config.Consensus.Clique.Signer.PrivateKey = v);
            Apply(options.InitialSigners, v => config.Consensus.Clique.InitialSigners = v ?? Array.Empty<string>());
            Apply(options.CliquePeriod, v => config.Consensus.Clique.PeriodSeconds = v);
            Apply(options.CliqueEpoch, v => config.Consensus.Clique.EpochLength = v);

            Apply(options.L1Rpc, v => config.Anchoring.L1RpcUrl = v);
            Apply(options.AnchorContract, v => config.Anchoring.ContractAddress = v);

            Apply(options.EnableMessaging, v => config.Messaging.Enabled = v);
            Apply(options.HubSourceChains, v => config.Messaging.HubSourceChains = v ?? Array.Empty<string>());
            Apply(options.MessagePollInterval, v => config.Messaging.PollIntervalMs = v);
            Apply(options.MaxMessagesPerPoll, v => config.Messaging.MaxMessagesPerPoll = v);
            Apply(options.EnableAcknowledgment, v => config.Messaging.AcknowledgmentEnabled = v);
            Apply(options.AcknowledgmentInterval, v => config.Messaging.AcknowledgmentIntervalMs = v);

            Apply(options.DeployMud, v => config.Mud.DeployWorld = v);
            Apply(options.WorldSalt, v =>
            {
                if (!string.IsNullOrEmpty(v))
                    config.Mud.WorldSalt = v.HexToByteArray();
            });

            return config;
        }

        public static AppChainServerConfig ParseConfig(string[] args)
        {
            var (root, options) = CreateRootCommand();
            var parseResult = root.Parse(args);
            var layered = BuildLayeredConfiguration(args);
            return BuildConfig(parseResult, options, layered);
        }

        public static bool TryHandleAdvancedHelp(string[] args, TextWriter writer)
        {
            if (!args.Any(a => a == "--help-advanced")) return false;
            PrintAdvancedHelp(writer);
            return true;
        }

        public static void PrintAdvancedHelp(TextWriter writer)
        {
            writer.WriteLine($"Nethereum AppChain Server v{Nethereum.CoreChain.NodeVersion.Version}");
            writer.WriteLine("Expert AppChain:* settings — raw form only (or, where noted above, a friendly flag).");
            writer.WriteLine();
            writer.WriteLine("USAGE: nethereum-appchain --AppChain:<Path> <value> [...]");
            writer.WriteLine();

            ChainNodeConfigSurface.RenderAdvancedHelp(writer, "AppChain", ChainNodeKind.AppChain);

            writer.WriteLine();
            writer.WriteLine("Settings can also come from appsettings.json (AppChain section) or AppChain__ env vars.");
            writer.WriteLine("Run with --help for the everyday flags.");
        }
    }
}
