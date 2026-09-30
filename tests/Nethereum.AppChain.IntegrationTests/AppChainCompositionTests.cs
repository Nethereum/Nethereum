using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainCompositionTests
    {
        [Fact]
        public Task Given_TheProductionAppChainComposition_When_ATestComposesANode_Then_ItGetsTheSameObjectsRunAsyncBuilds() =>
            ComposeAndRunAsync(IsolatedConfig(), composed =>
            {
                Assert.IsType<InstrumentedSequencer>(composed.Sequencer);
                Assert.Same(composed.AppChain, composed.Node.AppChain);
                Assert.Same(composed.Sequencer, composed.Node.Sequencer);
                Assert.NotNull(composed.ChainNode.Bundle);
                Assert.NotNull(composed.StateRootCalculator);
                Assert.NotNull(composed.SequencerConfig);
                return Task.CompletedTask;
            });

        [Fact]
        public Task Given_AHandRolledSequencer_When_BuiltOutsideComposition_Then_ItIsNotTheProductionObjectGraph() =>
            ComposeAndRunAsync(IsolatedConfig(), composed =>
            {
                var handRolled = new Nethereum.AppChain.Sequencer.Sequencer(composed.AppChain, SequencerConfig.OnDemand);

                Assert.IsNotType<InstrumentedSequencer>(handRolled);
                return Task.CompletedTask;
            });

        [Fact]
        public Task Given_AnAppChainTestFixture_When_ItDoesNotOverrideAField_Then_TheValueIsProductionsDefault() =>
            ComposeAndRunAsync(IsolatedConfig(), composed =>
            {
                Assert.Equal(BlockProductionMode.Interval, composed.SequencerConfig!.BlockProductionMode);
                Assert.False(composed.SequencerConfig.Policy.Enabled);
                return Task.CompletedTask;
            });

        [Fact]
        public Task Given_AnAppChainTestFixture_When_ItOverridesAField_Then_TheOverrideFlowsThroughComposition()
        {
            var config = IsolatedConfig();
            config.Consensus.Policy = PolicyConfig.RestrictedAccess(
                new List<string> { "0x1111111111111111111111111111111111111111" });

            return ComposeAndRunAsync(config, composed =>
            {
                Assert.True(composed.SequencerConfig!.Policy.Enabled);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Given_TheMudIntegrationFixture_When_ItComposesFromProduction_Then_BlockProductionModeIsNotSilentlyWrong()
        {
            var config = IsolatedConfig();
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Consensus.BlockTimeMs = 0;

            return ComposeAndRunAsync(config, async composed =>
            {
                var ownerAccount = new Account(config.Genesis.Owner.PrivateKey, (int)config.ChainId);
                var rpcClient = new AppChainRpcClient(composed.Node, (long)config.ChainId);
                var web3 = new Web3.Web3(ownerAccount, rpcClient);
                web3.TransactionManager.UseLegacyAsDefault = true;

                var transferService = new EtherTransferService(web3.TransactionManager);
                var transferTask = transferService.TransferEtherAndWaitForReceiptAsync(
                    "0x2222222222222222222222222222222222222222", 0.01m, gasPriceGwei: 0, gas: 21000);

                var winner = await Task.WhenAny(transferTask, Task.Delay(TimeSpan.FromSeconds(15)));

                Assert.True(winner == transferTask,
                    "BlockProductionMode.OnDemand composed from production must auto-produce a block " +
                    "when a transaction is submitted, not silently sit in the pool");
                var receipt = await transferTask;
                Assert.True(receipt.Succeeded());
            });
        }

        [Fact]
        public Task Given_RpcCapsSetOnNodeConfig_When_Composed_Then_TheyFlowThroughToTheChainConfigLogQueryGuardsRead()
        {
            var config = IsolatedConfig();
            config.Node.Rpc.MaxLogBlockRange = 42;
            config.Node.Rpc.MaxLogResults = 7;
            config.Node.Rpc.GasCap = 123_456;

            return ComposeAndRunAsync(config, composed =>
            {
                Assert.Equal(42, composed.AppChain.Config.RpcMaxLogBlockRange);
                Assert.Equal(7, composed.AppChain.Config.RpcMaxLogResults);
                Assert.Equal((BigInteger)123_456, composed.AppChain.Config.RpcGasCap);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Given_RpcCapsLeftUnset_When_Composed_Then_TheChainConfigKeepsProductionDefaults()
        {
            var config = IsolatedConfig();

            return ComposeAndRunAsync(config, composed =>
            {
                Assert.Equal(10_000, composed.AppChain.Config.RpcMaxLogBlockRange);
                Assert.Equal(10_000, composed.AppChain.Config.RpcMaxLogResults);
                Assert.Equal((BigInteger)50_000_000, composed.AppChain.Config.RpcGasCap);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public void Given_IntervalModeWithZeroBlockTime_When_ComposedFromProduction_Then_ValidationFailsLoudRatherThanHangingSilently()
        {
            var config = IsolatedConfig();
            config.Consensus.BlockTimeMs = 0;

            var ex = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("Block time", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task ComposeAndRunAsync(AppChainServerConfig config, Func<AppChainComposedNode, Task> body)
        {
            var signRecoverableBeforeCompose = EthECKey.SignRecoverable;
            var composed = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
            try
            {
                await body(composed);
            }
            finally
            {
                await composed.DisposeAsync();
                EthECKey.SignRecoverable = signRecoverableBeforeCompose;
            }
        }

        private static AppChainServerConfig IsolatedConfig()
        {
            var chainId = new BigInteger(420420_900 + Environment.TickCount % 1000);
            var ownerKey = EthECKey.GenerateKey();
            var sequencerKey = EthECKey.GenerateKey();

            var config = new AppChainServerConfig
            {
                ChainId = chainId,
                ChainName = "CompositionTest"
            };
            config.Genesis.Owner.PrivateKey = ownerKey.GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = sequencerKey.GetPrivateKey();
            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = false;
            config.Node.Sync.Mode = SyncMode.None;
            config.Mud.DeployWorld = false;
            return config;
        }
    }
}
