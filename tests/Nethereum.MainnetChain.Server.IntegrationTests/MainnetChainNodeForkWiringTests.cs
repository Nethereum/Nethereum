using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.MainnetChain;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class MainnetChainNodeForkWiringTests
    {
        private sealed class AcceptingGate : IConsensusBlockGate
        {
            public Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
                BlockHeader header, byte[] computedBlockHash, CancellationToken ct)
                => Task.FromResult(ConsensusBlockGateResult.Accept());
        }

        private sealed class EmptyBlockSource : IBlockSource
        {
            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await Task.CompletedTask;
                yield break;
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(default(BlockSourceHealth));

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
                => Task.CompletedTask;

            public DivergenceSignal LastChainBreak => null;
        }

        private sealed class ContinuePolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
                => ValidationAction.Continue;
        }

        private static FollowerChainNode BuildNode()
        {
            var factory = new MainnetChainNodeFactory(new AcceptingGate());
            return factory.Build(
                InMemoryChainStoreBundle.Open(),
                new EmptyBlockSource(),
                new ContinuePolicy(),
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0));
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public void Given_TheMainnetFollower_When_Built_Then_ItsConfigCarriesTheRealForkSchedule()
        {
            var config = BuildNode().Config;

            Assert.Same(MainnetChainActivations.Instance, config.Activations);

            Assert.Equal(
                HardforkName.OsakaBpo2,
                config.ResolveHardforkAt(24_179_383, MainnetChainActivations.OsakaBpo2Timestamp.Value));
            Assert.NotEqual(
                HardforkNames.Parse(config.Hardfork),
                config.ResolveHardforkAt(24_179_383, MainnetChainActivations.OsakaBpo2Timestamp.Value));
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public void Given_TheMainnetFollower_When_Built_Then_ItAnswersWithTheNativePrecompileBackends()
        {
            var config = BuildNode().Config;

            Assert.Same(MainnetChainHardforkRegistry.Instance, config.Registry);
            Assert.NotSame(
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance,
                config.Registry);
        }
    }
}
