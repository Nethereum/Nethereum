using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainRocksDbFlushCadenceTests
    {
        private const int BlocksProduced = 6;
        private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public async Task Given_ARocksDbFollowerAtCadenceK_When_ItImportsBlocksProducedByAnInMemoryReference_Then_EveryHashMatchesAndTheArmedTrieWritesArePendingExactlyBetweenBoundaries(int cadence)
        {
            var root = Path.Combine(Path.GetTempPath(), "appchain-cadence-" + Guid.NewGuid().ToString("N"));
            var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
            var chainId = new BigInteger(420430_000 + Environment.TickCount % 1000);
            var ownerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();
            var producerNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);

            var producerConfig = BuildConfig(
                chainId, ownerKey, sequencerKey, hasSigningKey: true, producerNodeKey, ports[0],
                Array.Empty<string>(), dataDirectory: null, cadence: 1);
            AppChainComposedNode? producer = null;
            AppChainComposedNode? follower = null;
            try
            {
                producer = await AppChainComposition.ComposeAsync(producerConfig, loggerFactory, CancellationToken.None);
                for (var i = 0; i < BlocksProduced; i++)
                    await producer.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, ownerKey, i));

                var followerConfig = BuildConfig(
                    chainId, ownerKey, sequencerKey, hasSigningKey: false, followerNodeKey, ports[1],
                    new[] { EnodeOf(producerNodeKey, ports[0]) }, dataDirectory: Path.Combine(root, "follower"), cadence);
                follower = await AppChainComposition.ComposeAsync(followerConfig, loggerFactory, CancellationToken.None);

                Assert.True(await WaitUntilAsync(async () => await follower.Bundle.Blocks.GetHeightAsync() >= BlocksProduced, SettleTimeout),
                    "the follower never imported the produced blocks");

                for (var n = 1; n <= BlocksProduced; n++)
                {
                    var expected = await producer.Bundle.Blocks.GetHashByNumberAsync(n);
                    var actual = await follower.Bundle.Blocks.GetHashByNumberAsync(n);
                    Assert.Equal(expected.ToHex(), actual.ToHex());
                }

                var pending = ((CapturingJournalingPathNodeStore)follower.Bundle.StateTrieNodes).TakeCaptured();
                if (BlocksProduced % cadence == 0)
                    Assert.Empty(pending.Nodes);
                else
                    Assert.NotEmpty(pending.Nodes);
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                if (producer != null) await producer.DisposeAsync();
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        public async Task Given_ARocksDbProducerAtCadenceK_When_ItProducesBlocks_Then_AnInMemoryFollowerAgreesOnEveryHashAndTheProducersArmedTrieWritesArePendingExactlyBetweenBoundaries(int cadence)
        {
            var root = Path.Combine(Path.GetTempPath(), "appchain-producer-cadence-" + Guid.NewGuid().ToString("N"));
            var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
            var chainId = new BigInteger(420440_000 + Environment.TickCount % 1000);
            var ownerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();
            var producerNodeKey = EthECKey.GenerateKey();
            var followerNodeKey = EthECKey.GenerateKey();
            var ports = ReserveFreeLoopbackPorts(2);

            var producerConfig = BuildConfig(
                chainId, ownerKey, sequencerKey, hasSigningKey: true, producerNodeKey, ports[0],
                Array.Empty<string>(), dataDirectory: Path.Combine(root, "producer"), cadence);
            AppChainComposedNode? producer = null;
            AppChainComposedNode? follower = null;
            try
            {
                producer = await AppChainComposition.ComposeAsync(producerConfig, loggerFactory, CancellationToken.None);
                for (var i = 0; i < BlocksProduced; i++)
                    await producer.Sequencer!.SubmitTransactionAsync(SignedTransfer(chainId, ownerKey, i));

                var followerConfig = BuildConfig(
                    chainId, ownerKey, sequencerKey, hasSigningKey: false, followerNodeKey, ports[1],
                    new[] { EnodeOf(producerNodeKey, ports[0]) }, dataDirectory: null, cadence: 1);
                follower = await AppChainComposition.ComposeAsync(followerConfig, loggerFactory, CancellationToken.None);

                Assert.True(await WaitUntilAsync(async () => await follower.Bundle.Blocks.GetHeightAsync() >= BlocksProduced, SettleTimeout),
                    "the follower never imported the produced blocks");

                for (var n = 1; n <= BlocksProduced; n++)
                {
                    var expected = await producer.Bundle.Blocks.GetHashByNumberAsync(n);
                    var actual = await follower.Bundle.Blocks.GetHashByNumberAsync(n);
                    Assert.Equal(expected.ToHex(), actual.ToHex());
                }

                var pending = ((CapturingJournalingPathNodeStore)producer.Bundle.StateTrieNodes).TakeCaptured();
                if (BlocksProduced % cadence == 0)
                    Assert.Empty(pending.Nodes);
                else
                    Assert.NotEmpty(pending.Nodes);
            }
            finally
            {
                if (follower != null) await follower.DisposeAsync();
                if (producer != null) await producer.DisposeAsync();
                try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static AppChainServerConfig BuildConfig(
            BigInteger chainId, EthECKey ownerKey, EthECKey sequencerKey, bool hasSigningKey,
            EthECKey nodeKey, int listenPort, string[] trustedPeers, string? dataDirectory, int cadence)
        {
            var config = new AppChainServerConfig { ChainId = chainId, ChainName = "AppChainCadenceE2E" };
            config.Genesis.Owner.PrivateKey = ownerKey.GetPrivateKey();
            config.Consensus.Mode = AppChainConsensusMode.SingleSequencer;
            config.Consensus.AllowEmptyBlocks = true;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            if (hasSigningKey)
                config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            else
                config.Consensus.Sequencer.Address = sequencerKey.GetPublicAddress();
            config.Node.Storage.InMemory = dataDirectory == null;
            if (dataDirectory != null)
            {
                config.Node.Storage.DataDirectory = dataDirectory;
                config.Node.Storage.PathKeyedState = true;
                config.Node.Storage.TrieNodeHistoryBlocks = 128;
                config.Node.Storage.BlockCacheSize = 32L * 1024 * 1024;
            }
            config.Node.Storage.FlushCadenceBlocks = cadence;
            config.Node.Network.Serve = true;
            config.Node.Network.NodeKeyHex = nodeKey.GetPrivateKey();
            config.Node.Network.ListenPort = listenPort;
            config.Node.Network.BindAddress = IPAddress.Loopback;
            config.Node.Network.TrustedPeers = trustedPeers;
            config.Node.Network.TargetPeerCount = Math.Max(1, trustedPeers.Length);
            config.Node.Network.DialBudgetPerSecond = 0;
            config.Node.Network.MaxPeersPerIPv4Subnet = 0;
            config.Node.Network.MaxPeersPerIPv6Subnet = 0;
            config.Node.Network.MaxInboundPeers = 4;
            config.Node.Network.MaxInboundPerIP = 4;
            config.Node.Sync.Mode = SyncMode.ForwardExecute;
            config.Mud.DeployWorld = false;
            return config;
        }

        private static ISignedTransaction SignedTransfer(BigInteger chainId, EthECKey sender, int nonce)
        {
            var tx = new Transaction1559(
                chainId: chainId, nonce: nonce, maxPriorityFeePerGas: BigInteger.Zero, maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(1000000), receiverAddress: "0x000000000000000000000000000000000000dEaD",
                amount: BigInteger.One, data: null, accessList: null);
            var signature = sender.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return tx;
        }

        private static string EnodeOf(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static IReadOnlyList<int> ReserveFreeLoopbackPorts(int count)
        {
            var ports = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
                listener.Stop();
            }
            return ports;
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false)) return true;
                await Task.Delay(100).ConfigureAwait(false);
            }
            return await condition().ConfigureAwait(false);
        }
    }
}
