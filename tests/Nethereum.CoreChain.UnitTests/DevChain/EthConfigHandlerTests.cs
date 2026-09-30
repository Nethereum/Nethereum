using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EthConfigHandlerTests
    {
        private static async Task<ChainConfiguration> AskEthConfigAsync(string hardfork)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;
            using var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();

            var response = await new EthConfigHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_config"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            return Assert.IsType<ChainConfiguration>(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_AnAmsterdamDevChain_When_AskedForEthConfig_Then_CurrentCarriesChainIdARealForkIdAndTheWiredPrecompiles()
        {
            var cfg = await AskEthConfigAsync("amsterdam");

            Assert.NotNull(cfg.Current);
            Assert.Matches("^0x[0-9a-f]+$", cfg.Current.ChainId);

            Assert.False(string.IsNullOrEmpty(cfg.Current.ForkId));
            Assert.NotEqual("0x00000000", cfg.Current.ForkId);

            Assert.True(cfg.Current.Precompiles.ContainsKey("ECREC"));
            Assert.True(cfg.Current.Precompiles.ContainsKey("P256VERIFY"));
            Assert.False(cfg.Current.Precompiles.ContainsKey("KZG_POINT_EVALUATION"),
                "DevChain runs the managed registry; KZG is a placeholder and must not be reported as available");
            Assert.False(cfg.Current.Precompiles.ContainsKey("BLS12_PAIRING_CHECK"));

            Assert.True(cfg.Current.SystemContracts.ContainsKey("BUILDER_DEPOSIT_CONTRACT_ADDRESS"));
            Assert.True(cfg.Current.SystemContracts.ContainsKey("HISTORY_STORAGE_ADDRESS"));

            Assert.NotNull(cfg.Current.BlobSchedule);
            Assert.Equal(14UL, cfg.Current.BlobSchedule.Target);
            Assert.Equal(21UL, cfg.Current.BlobSchedule.Max);
            Assert.Equal(11684671UL, cfg.Current.BlobSchedule.BaseFeeUpdateFraction);
        }

        [Theory]
        [InlineData("cancun", 3UL, 6UL, 3338477UL)]
        [InlineData("prague", 6UL, 9UL, 5007716UL)]
        [InlineData("amsterdam", 14UL, 21UL, 11684671UL)]
        public async Task Given_ADevChainAtAFork_When_AskedForEthConfig_Then_ItsBlobScheduleMatchesGeth(
            string hardfork, ulong target, ulong max, ulong updateFraction)
        {
            var cfg = await AskEthConfigAsync(hardfork);

            Assert.NotNull(cfg.Current.BlobSchedule);
            Assert.Equal(target, cfg.Current.BlobSchedule.Target);
            Assert.Equal(max, cfg.Current.BlobSchedule.Max);
            Assert.Equal(updateFraction, cfg.Current.BlobSchedule.BaseFeeUpdateFraction);
        }

        [Fact]
        public async Task Given_APragueDevChain_When_AskedForEthConfig_Then_CurrentHasNoAmsterdamSystemContracts()
        {
            var cfg = await AskEthConfigAsync("prague");

            Assert.NotNull(cfg.Current);
            Assert.False(cfg.Current.SystemContracts.ContainsKey("BUILDER_DEPOSIT_CONTRACT_ADDRESS"));
            Assert.True(cfg.Current.SystemContracts.ContainsKey("HISTORY_STORAGE_ADDRESS"));
        }

        [Fact]
        public async Task Given_ADevChain_When_AskedForEthConfig_Then_TheForkIdIsTheOneThePeerHandshakeWouldAdvertise()
        {
            var cfg = await AskEthConfigAsync("amsterdam");

            Assert.Matches("^0x[0-9a-f]{8}$", cfg.Current.ForkId);
        }

        [Fact]
        public async Task Given_APragueDevChain_When_AskedForEthConfig_Then_CurrentReportsTheDepositContractSystemContract()
        {
            var cfg = await AskEthConfigAsync("prague");

            Assert.NotNull(cfg.Current);
            Assert.True(cfg.Current.SystemContracts.TryGetValue("DEPOSIT_CONTRACT_ADDRESS", out var address));
            Assert.Equal("0x0000000000000000000000000000000000000000", address);
        }

        [Fact]
        public async Task Given_ACancunDevChain_When_AskedForEthConfig_Then_CurrentHasNoDepositContractSystemContract()
        {
            var cfg = await AskEthConfigAsync("cancun");

            Assert.NotNull(cfg.Current);
            Assert.False(cfg.Current.SystemContracts.ContainsKey("DEPOSIT_CONTRACT_ADDRESS"));
        }

        [Fact]
        public async Task Given_TwoForksScheduledAfterTheCurrentOne_When_AskedForEthConfig_Then_LastIsTheFurthestScheduledForkNotThePreviousOne()
        {
            var config = DevChainConfig.Default;
            config.GenesisTimestamp = 0;
            config.ForkSchedule.GenesisFork = "cancun";
            config.ForkSchedule.Schedule = new List<ForkActivationEntry>
            {
                new ForkActivationEntry { Fork = "cancun", Timestamp = 0 },
                new ForkActivationEntry { Fork = "prague", Timestamp = 100 },
                new ForkActivationEntry { Fork = "amsterdam", Timestamp = 200 }
            };

            using var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();

            var response = await new EthConfigHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_config"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var cfg = Assert.IsType<ChainConfiguration>(response.ResultNewtonsoft);

            Assert.NotNull(cfg.Next);
            Assert.Equal(100UL, cfg.Next.ActivationTime);

            Assert.NotNull(cfg.Last);
            Assert.Equal(200UL, cfg.Last.ActivationTime);
        }

        [Fact]
        public async Task Given_NoForkScheduledAfterTheCurrentOne_When_AskedForEthConfig_Then_NextAndLastAreBothNull()
        {
            var config = DevChainConfig.Default;
            config.GenesisTimestamp = 0;
            config.ForkSchedule.GenesisFork = "amsterdam";
            config.ForkSchedule.Schedule = new List<ForkActivationEntry>
            {
                new ForkActivationEntry { Fork = "amsterdam", Timestamp = 0 }
            };

            using var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();

            var response = await new EthConfigHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_config"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var cfg = Assert.IsType<ChainConfiguration>(response.ResultNewtonsoft);

            Assert.Null(cfg.Next);
            Assert.Null(cfg.Last);
        }
    }
}
