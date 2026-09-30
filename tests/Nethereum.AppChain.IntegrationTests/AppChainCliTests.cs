using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using Nethereum.AppChain.Server;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainCliTests
    {
        private static string AppSettingsPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        private static void WithAppSettingsFile(string json, Action test)
        {
            var path = AppSettingsPath;
            File.WriteAllText(path, json);
            try
            {
                test();
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void WithEnvironmentVariable(string name, string value, Action test)
        {
            Environment.SetEnvironmentVariable(name, value);
            try
            {
                test();
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [Fact]
        public void Given_AConfigFileHost_When_TheCliAlsoPassesHost_Then_TheCliValueWins()
        {
            WithAppSettingsFile(@"{ ""AppChain"": { ""Node"": { ""Rpc"": { ""Host"": ""10.0.0.5"" } } } }", () =>
            {
                var config = AppChainCli.ParseConfig(new[] { "--host", "192.168.1.1" });

                Assert.Equal("192.168.1.1", config.Node.Rpc.Host);
            });
        }

        [Fact]
        public void Given_AConfigFileHost_When_TheCliDoesNotPassHost_Then_TheFileValueIsUsed()
        {
            WithAppSettingsFile(@"{ ""AppChain"": { ""Node"": { ""Rpc"": { ""Host"": ""10.0.0.5"" } } } }", () =>
            {
                var config = AppChainCli.ParseConfig(Array.Empty<string>());

                Assert.Equal("10.0.0.5", config.Node.Rpc.Host);
            });
        }

        [Fact]
        public void Given_AnAppChainPrefixedEnvVar_When_ConfigIsParsed_Then_TheEnvValueIsBound()
        {
            WithEnvironmentVariable("AppChain__ChainName", "EnvChain", () =>
            {
                var config = AppChainCli.ParseConfig(Array.Empty<string>());

                Assert.Equal("EnvChain", config.ChainName);
            });
        }

        [Fact]
        public void Given_AnAppChainPrefixedEnvVar_When_TheCliAlsoPassesTheFriendlyFlag_Then_TheCliValueWins()
        {
            WithEnvironmentVariable("AppChain__ChainName", "EnvChain", () =>
            {
                var config = AppChainCli.ParseConfig(new[] { "--name", "CliChain" });

                Assert.Equal("CliChain", config.ChainName);
            });
        }

        [Fact]
        public void Given_TheNodeKeyHexFlag_When_ConfigIsParsed_Then_ItPinsTheNetworkNodeIdentity()
        {
            const string hex = "0x1111111111111111111111111111111111111111111111111111111111111111";

            var config = AppChainCli.ParseConfig(new[] { "--node-key-hex", hex });

            Assert.Equal(hex, config.Node.Network.NodeKeyHex);
        }

        [Fact]
        public void Given_TheNodeKeyFileFlag_When_ConfigIsParsed_Then_ItPinsThePersistedNodeIdentityFile()
        {
            var config = AppChainCli.ParseConfig(new[] { "--node-key-file", "/data/appchain/nodekey" });

            Assert.Equal("/data/appchain/nodekey", config.Node.Network.NodeKeyFile);
        }

        [Fact]
        public void Given_NoDeployMudWorldFlag_When_ConfigIsParsed_Then_TheMudWorldIsDeployed()
        {
            var config = AppChainCli.ParseConfig(Array.Empty<string>());

            Assert.True(config.Mud.DeployWorld);
        }

        [Fact]
        public void Given_DeployMudWorldFalse_When_ConfigIsParsed_Then_TheMudWorldIsNotDeployed()
        {
            var config = AppChainCli.ParseConfig(new[] { "--deploy-mud-world", "false" });

            Assert.False(config.Mud.DeployWorld);
        }

        [Fact]
        public void Given_TheDeployMudWorldOption_When_HelpIsBuilt_Then_ItsDefaultMatchesTheEffectiveDefault()
        {
            var options = new AppChainCliOptions();

            var parsed = new RootCommand { options.DeployMud }.Parse(Array.Empty<string>());

            Assert.True(parsed.GetValueForOption(options.DeployMud));
        }

        [Fact]
        public void Given_NoDbPathOverride_When_ConfigIsParsed_Then_TheDefaultDataDirectoryIsAppChainData()
        {
            var config = AppChainCli.ParseConfig(Array.Empty<string>());

            Assert.Equal("./appchain-data", config.Node.Storage.DataDirectory);
        }

        [Fact]
        public void Given_ARawAppChainColonOverrideOnTheCommandLine_When_ConfigIsParsed_Then_ItBindsIntoTheAdvancedSurface()
        {
            var config = AppChainCli.ParseConfig(new[] { "--AppChain:Node:Rpc:MetricsPort", "9464" });

            Assert.Equal(9464, config.Node.Rpc.MetricsPort);
        }

        [Fact]
        public void Given_TheHelpAdvancedFlag_When_Checked_Then_ItPrintsTheAdvancedChainNodeKnobsAndReturnsTrue()
        {
            var writer = new StringWriter();

            var handled = AppChainCli.TryHandleAdvancedHelp(new[] { "--help-advanced" }, writer);

            Assert.True(handled);
            var output = writer.ToString();
            Assert.Contains("--AppChain:Node:Sync:StartBlock", output);
            Assert.Contains("--AppChain:Node:Storage:BlockCacheSize", output);
            Assert.Contains("--AppChain:Node:Maintenance:WipeState", output);
        }

        [Fact]
        public void Given_NoHelpAdvancedFlag_When_Checked_Then_ItReturnsFalseAndPrintsNothing()
        {
            var writer = new StringWriter();

            var handled = AppChainCli.TryHandleAdvancedHelp(new[] { "--host", "127.0.0.1" }, writer);

            Assert.False(handled);
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void Given_TheDbPathFlag_When_ConfigIsParsed_Then_ItSetsTheDataDirectory()
        {
            var config = AppChainCli.ParseConfig(new[] { "--db-path", "./from-db-path" });

            Assert.Equal("./from-db-path", config.Node.Storage.DataDirectory);
        }

        [Fact]
        public void Given_TheDataDirAlias_When_ConfigIsParsed_Then_ItSetsTheSameDataDirectoryAsDbPath()
        {
            var config = AppChainCli.ParseConfig(new[] { "--data-dir", "./from-data-dir" });

            Assert.Equal("./from-data-dir", config.Node.Storage.DataDirectory);
        }

        [Fact]
        public void Given_BothDbPathAndDataDirAliasWithDifferentValues_When_Parsed_Then_TheParserRejectsTheAmbiguity()
        {
            var (root, _) = AppChainCli.CreateRootCommand();

            var parseResult = root.Parse(new[] { "--db-path", "./first", "--data-dir", "./second" });

            Assert.NotEmpty(parseResult.Errors);
        }

        [Fact]
        public void Given_TheDevP2PPeersFlag_When_ConfigIsParsed_Then_ItSetsTheTrustedPeers()
        {
            var config = AppChainCli.ParseConfig(new[] { "--devp2p-peers", "enode://aaaa@127.0.0.1:30401" });

            Assert.Equal(new[] { "enode://aaaa@127.0.0.1:30401" }, config.Node.Network.TrustedPeers);
        }

        [Fact]
        public void Given_TheTrustedPeerAlias_When_ConfigIsParsed_Then_ItSetsTheSameTrustedPeersAsDevP2PPeers()
        {
            var config = AppChainCli.ParseConfig(new[] { "--trusted-peer", "enode://bbbb@127.0.0.1:30402" });

            Assert.Equal(new[] { "enode://bbbb@127.0.0.1:30402" }, config.Node.Network.TrustedPeers);
        }

        [Fact]
        public void Given_TheTrustedPeerAliasRepeated_When_ConfigIsParsed_Then_AllPeersAreCollected()
        {
            var config = AppChainCli.ParseConfig(new[]
            {
                "--trusted-peer", "enode://cccc@127.0.0.1:30403",
                "--trusted-peer", "enode://dddd@127.0.0.1:30404"
            });

            Assert.Equal(
                new[] { "enode://cccc@127.0.0.1:30403", "enode://dddd@127.0.0.1:30404" },
                config.Node.Network.TrustedPeers);
        }

        [Fact]
        public void Given_TheDevP2PServePortFlag_When_ConfigIsParsed_Then_ItSetsTheListenPort()
        {
            var config = AppChainCli.ParseConfig(new[] { "--devp2p-serve-port", "40500" });

            Assert.Equal(40500, config.Node.Network.ListenPort);
        }

        [Fact]
        public void Given_TheListenPortAlias_When_ConfigIsParsed_Then_ItSetsTheSameListenPortAsDevP2PServePort()
        {
            var config = AppChainCli.ParseConfig(new[] { "--listen-port", "40501" });

            Assert.Equal(40501, config.Node.Network.ListenPort);
        }

        [Fact]
        public void Given_NoAliasFlags_When_ConfigIsParsed_Then_ExistingFlagsStillProduceTheirOwnDefaults()
        {
            var config = AppChainCli.ParseConfig(new[] { "--enable-devp2p-serve", "true" });

            Assert.True(config.Node.Network.Serve);
            Assert.Equal(30403, config.Node.Network.ListenPort);
            Assert.Equal("./appchain-data", config.Node.Storage.DataDirectory);
            Assert.Empty(config.Node.Network.TrustedPeers);
        }
    }
}
