using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S6
{
    public sealed class S6ProducerHost : IAsyncDisposable
    {
        private static int _chainCounter;

        public AppChainComposedNode Node { get; private set; } = null!;
        public EthECKey OwnerKey { get; private set; } = null!;
        public EthECKey SequencerKey { get; private set; } = null!;
        public BigInteger ChainId { get; private set; }
        public AppChainServerConfig Config { get; private set; } = null!;

        public static async Task<S6ProducerHost> StartAsync(
            Action<AppChainServerConfig>? configure = null, bool serve = false, int? listenPort = null,
            EthECKey? ownerKey = null, EthECKey? sequencerKey = null, BigInteger? chainId = null,
            EthECKey? nodeKey = null)
        {
            var host = new S6ProducerHost
            {
                OwnerKey = ownerKey ?? EthECKey.GenerateKey(),
                SequencerKey = sequencerKey ?? EthECKey.GenerateKey(),
                ChainId = chainId ?? new BigInteger(421600_000 + Interlocked.Increment(ref _chainCounter))
            };
            var config = new AppChainServerConfig { ChainId = host.ChainId, ChainName = "S6Audit" };
            config.Genesis.Owner.PrivateKey = host.OwnerKey.GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = host.SequencerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = false;
            config.Consensus.BlockTimeMs = 3_600_000;
            config.Consensus.BlockProductionMode = BlockProductionMode.Interval;
            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = serve;
            if (serve)
            {
                config.Node.Network.NodeKeyHex = (nodeKey ?? EthECKey.GenerateKey()).GetPrivateKey();
                config.Node.Network.ListenPort = listenPort ?? FreePort();
                config.Node.Network.BindAddress = IPAddress.Loopback;
                config.Node.Network.DialBudgetPerSecond = 0;
                config.Node.Network.MaxPeersPerIPv4Subnet = 0;
                config.Node.Network.MaxPeersPerIPv6Subnet = 0;
                config.Node.Network.MaxInboundPeers = 4;
                config.Node.Network.MaxInboundPerIP = 4;
            }
            config.Node.Sync.Mode = SyncMode.None;
            config.Mud.DeployWorld = false;
            configure?.Invoke(config);
            host.Config = config;
            host.Node = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
            return host;
        }

        public static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public string OwnerAddress => OwnerKey.GetPublicAddress();

        public ISignedTransaction Legacy(EthECKey key, string to, BigInteger nonce, long gasLimit = 21_000, string data = "", BigInteger? chainIdOverride = null, BigInteger? gasPrice = null)
        {
            var hex = new LegacyTransactionSigner().SignTransaction(
                key.GetPrivateKey().HexToByteArray(), chainIdOverride ?? ChainId, to, BigInteger.Zero, nonce,
                gasPrice ?? BigInteger.Zero, gasLimit, data);
            return TransactionFactory.CreateTransaction(hex.EnsureHexPrefix());
        }

        public ISignedTransaction Eip1559(EthECKey key, string to, BigInteger nonce, long gasLimit = 21_000, string data = "", BigInteger? chainIdOverride = null)
        {
            var tx = new Transaction1559(
                (Nethereum.Util.EvmUInt256)(chainIdOverride ?? ChainId), (Nethereum.Util.EvmUInt256)nonce,
                Nethereum.Util.EvmUInt256.Zero, Nethereum.Util.EvmUInt256.Zero,
                (Nethereum.Util.EvmUInt256)gasLimit, to, Nethereum.Util.EvmUInt256.Zero, data,
                new List<AccessListItem>());
            new Transaction1559Signer().SignTransaction(key, tx);
            return tx;
        }

        public async ValueTask DisposeAsync()
        {
            if (Node != null) await Node.DisposeAsync();
        }
    }
}
