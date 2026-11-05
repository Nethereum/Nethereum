using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class CustomGenesisChainConfigTests
    {
        private sealed class AcceptingGate : IConsensusBlockGate
        {
            public Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
                Nethereum.Model.BlockHeader header, byte[] computedBlockHash, CancellationToken ct)
                => Task.FromResult(ConsensusBlockGateResult.Accept());
        }

        [Fact]
        public void Given_a_custom_genesis_When_the_chain_config_is_built_Then_the_chain_id_forks_and_blob_schedule_come_from_the_genesis()
        {
            var config = MainnetChainNodeFactory.BuildChainConfig(CustomGenesisFixture.Document());

            Assert.Equal(CustomGenesisFixture.ChainId, (long)config.ChainId);
            Assert.Equal(HardforkName.Osaka, config.ResolveHardforkAt(0, 0));
            Assert.Equal(HardforkName.Osaka, config.ResolveHardforkAt(1_000_000, 1_000_000));
            Assert.Equal(6, config.GetHardforkConfigAt(1, 1).TargetBlobsPerBlock);
            Assert.Equal(9, config.GetHardforkConfigAt(1, 1).MaxBlobsPerBlock);
            Assert.Equal(1_000_000_000, (long)config.BaseFee);
        }

        [Fact]
        public void Given_a_custom_genesis_that_activates_osaka_later_When_the_chain_config_is_built_Then_the_forks_before_it_resolve_by_timestamp()
        {
            var config = MainnetChainNodeFactory.BuildChainConfig(CustomGenesisFixture.Document(osakaTime: "100"));

            Assert.Equal(HardforkName.Prague, config.ResolveHardforkAt(1, 99));
            Assert.Equal(HardforkName.Osaka, config.ResolveHardforkAt(1, 100));
        }

        [Fact]
        public void Given_no_genesis_When_the_chain_config_is_built_Then_it_is_mainnet()
        {
            var config = MainnetChainNodeFactory.BuildChainConfig();

            Assert.Equal(1, (long)config.ChainId);
            Assert.Same(MainnetChainActivations.Instance, config.Activations);
        }

        [Fact]
        public void Given_a_custom_genesis_When_the_follower_node_is_built_Then_it_executes_under_the_genesis_fork_schedule()
        {
            var factory = new MainnetChainNodeFactory(
                new AcceptingGate(), loggerFactory: null, follower: null, flushCadence: null,
                prefetchWorkers: 0, stateRootWorkers: 1, genesis: CustomGenesisFixture.Document());

            var node = factory.Build(
                InMemoryChainStoreBundle.Open(), new LocalReplayBlockSource(new List<BlockBundle>()),
                new ContinuePolicy(), new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0));

            Assert.Equal(CustomGenesisFixture.ChainId, (long)node.Config.ChainId);
            Assert.Equal(HardforkName.Osaka, node.Config.ResolveHardforkAt(1, 12));
        }

        [Fact]
        public void Given_the_genesis_flag_When_the_cli_arguments_apply_Then_the_path_is_kept_and_the_engine_decides_the_mode()
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[] { "--genesis", "/genesis.json", "--engine" });

            Assert.Equal("/genesis.json", config.GenesisPath);
            Assert.True(config.EngineOnly);
        }

        [Fact]
        public void Given_flags_written_as_name_equals_value_When_the_cli_arguments_apply_Then_they_mean_the_same_as_the_two_argument_form()
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, new[]
            {
                "--genesis=/network-config/genesis.json", "--data-dir=/data", "--port=8545", "--host=0.0.0.0",
                "--engine", "--engine-port=8551", "--engine-jwt-secret=/tmp/jwtsecret", "--flush-cadence=8"
            });

            Assert.Equal("/network-config/genesis.json", config.GenesisPath);
            Assert.Equal("/data", config.DataDir);
            Assert.Equal(8545, config.Port);
            Assert.Equal("0.0.0.0", config.Host);
            Assert.Equal(8551, config.Engine.Port);
            Assert.Equal("/tmp/jwtsecret", config.Engine.JwtSecretPath);
            Assert.Equal(8, config.FlushCadenceBlocks);
            Assert.True(config.EngineOnly);
        }

        [Fact]
        public void Given_a_genesis_without_the_engine_When_the_startup_guard_evaluates_Then_the_start_is_refused()
        {
            var config = new MainnetChainServerConfig { GenesisPath = "/genesis.json", AllowUnverifiedConsensus = true };

            Assert.False(config.EngineOnly);
            Assert.Equal(ConsensusStartupGuard.Decision.RefuseGenesisWithoutEngine, ConsensusStartupGuard.Evaluate(config));
        }

        [Fact]
        public void Given_a_genesis_with_the_engine_When_the_startup_guard_evaluates_Then_the_engine_drives()
        {
            var config = new MainnetChainServerConfig { GenesisPath = "/genesis.json" };
            config.Engine.Enabled = true;

            Assert.Equal(ConsensusStartupGuard.Decision.EngineDriven, ConsensusStartupGuard.Evaluate(config));
        }

        private sealed class ContinuePolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }
    }
}
