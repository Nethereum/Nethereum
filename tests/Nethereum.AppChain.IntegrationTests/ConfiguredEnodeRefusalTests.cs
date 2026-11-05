using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class ConfiguredEnodeRefusalTests
    {
        private const string MalformedEnode = "enode://not-a-real-pubkey@127.0.0.1:30702";

        private static string WellFormedEnode(int port) =>
            $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static AppChainServerConfig SequencerOnDisk(string dataDirectory)
        {
            var config = new AppChainServerConfig
            {
                ChainId = new BigInteger(420420_700 + Environment.TickCount % 1000),
                ChainName = "EnodeRefusalTest",
            };
            config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Node.Storage.InMemory = false;
            config.Node.Storage.DataDirectory = dataDirectory;
            config.Node.Network.Serve = false;
            config.Node.Sync.Mode = SyncMode.None;
            config.Mud.DeployWorld = false;
            return config;
        }

        [Theory]
        [InlineData("TrustedPeers", "AppChain:Node:Network:TrustedPeers:1")]
        [InlineData("TrustedBootnodes", "AppChain:Node:Network:TrustedBootnodes:0")]
        [InlineData("FollowPeerEnode", "AppChain:Node:Sync:FollowPeerEnode")]
        public void Given_AMalformedEnodeUnderAConfigKey_When_TheAppChainConfigIsValidated_Then_ItRefusesNamingThatKeyAndTheEntry(
            string where, string expectedKey)
        {
            var config = SequencerOnDisk(Path.Combine(Path.GetTempPath(), "unused-" + Guid.NewGuid().ToString("N")));
            switch (where)
            {
                case "TrustedPeers":
                    config.Node.Network.TrustedPeers = new[] { WellFormedEnode(30701), MalformedEnode };
                    break;
                case "TrustedBootnodes":
                    config.Node.Network.TrustedBootnodes = new[] { MalformedEnode };
                    break;
                default:
                    config.Node.Sync.FollowPeerEnode = MalformedEnode;
                    break;
            }

            var refusal = Assert.Throws<InvalidOperationException>(() => config.Validate());

            Assert.Contains($"{expectedKey} = '{MalformedEnode}'", refusal.Message);
            Assert.DoesNotContain("comma", refusal.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Given_AMalformedTrustedPeer_When_TheAppChainComposesOnDisk_Then_StartupIsRefusedBeforeAnyDatabaseNodeKeyOrGenesisIsWritten()
        {
            var dataDirectory = Path.Combine(Path.GetTempPath(), "enode-refusal-" + Guid.NewGuid().ToString("N"));
            var config = SequencerOnDisk(dataDirectory);
            config.Node.Network.TrustedPeers = new[] { MalformedEnode };
            var signRecoverableBefore = EthECKey.SignRecoverable;
            try
            {
                var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None));

                Assert.Contains(MalformedEnode, refusal.Message);
                Assert.False(Directory.Exists(dataDirectory), "a refused start must not create the data directory");
            }
            finally
            {
                EthECKey.SignRecoverable = signRecoverableBefore;
                if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task Given_OnlyWellFormedTrustedPeers_When_TheAppChainComposesOnDisk_Then_ItStartsAndWritesItsDataDirectory()
        {
            var dataDirectory = Path.Combine(Path.GetTempPath(), "enode-refusal-" + Guid.NewGuid().ToString("N"));
            var config = SequencerOnDisk(dataDirectory);
            config.Node.Network.TrustedPeers = new[] { WellFormedEnode(30703) };
            var signRecoverableBefore = EthECKey.SignRecoverable;
            try
            {
                var composed = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
                try
                {
                    Assert.True(Directory.Exists(dataDirectory));
                    Assert.NotEmpty(Directory.GetFileSystemEntries(dataDirectory));
                }
                finally
                {
                    await composed.DisposeAsync();
                }
            }
            finally
            {
                EthECKey.SignRecoverable = signRecoverableBefore;
                if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }
}
