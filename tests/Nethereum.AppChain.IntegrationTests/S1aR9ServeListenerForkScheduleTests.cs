using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class S1aR9ServeListenerForkScheduleTests
    {
        private sealed class ScheduledProfile : IChainProfile
        {
            public ScheduledProfile(byte[] genesisHash) { GenesisHash = genesisHash; }
            public ulong NetworkId => 990501;
            public byte[] GenesisHash { get; }
            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
                (new ulong[] { 100UL, 200UL }, new ulong[] { 1_700_000_000UL });
            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();
            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                throw new NotSupportedException();
        }

        [Fact]
        public async Task S1aR9_04_Given_AChainProfileWithAForkSchedule_When_TheServeListenerStarts_Then_InboundStatusesAreCheckedAgainstThatSchedule()
        {
            var genesisHash = Enumerable.Range(0, 32).Select(i => (byte)(0x51 ^ i)).ToArray();
            var bundle = InMemoryChainStoreBundle.Open();
            await bundle.Blocks.SaveAsync(new BlockHeader { BlockNumber = 0, Timestamp = 1000 }, genesisHash);
            var profile = new ScheduledProfile(genesisHash);

            var config = new ChainNodeConfig();
            config.Network.ListenPort = 0;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.NodeKeyHex = EthECKey.GenerateKey().GetPrivateKey();
            var mempool = ChainNodeMempool.Create(profile, bundle, config, new Eth68PeerPool(), loggerFactory: null);

            var listener = await ChainNodeServeListener.StartAsync(
                profile, bundle, config, mempool, callbacks: null, loggerFactory: null);
            try
            {
                Assert.Equal(new ulong[] { 100UL, 200UL }, listener.Options.ForkBlockNumbers);
                Assert.Equal(new ulong[] { 1_700_000_000UL }, listener.Options.ForkTimestamps);
            }
            finally
            {
                await listener.DisposeAsync();
                mempool.Dispose();
                await bundle.DisposeAsync();
            }
        }
    }
}
