using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetEthConfigDepositContractTests
    {
        private sealed class NoOpBlockSource : IBlockSource
        {
            public IAsyncEnumerable<BlockBundle> StreamAsync(ulong fromBlock, CancellationToken ct) =>
                throw new NotImplementedException();

            public System.Threading.Tasks.Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) =>
                throw new NotImplementedException();

            public System.Threading.Tasks.Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) =>
                throw new NotImplementedException();

            public DivergenceSignal LastChainBreak => null;
        }

        private sealed class AlwaysContinuePolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;

            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        [Fact]
        public void Given_TheRealMainnetChainNodeFactory_When_ProjectingEthConfigAtPrague_Then_ItReportsTheRealMainnetDepositContractAddress()
        {
            var factory = new MainnetChainNodeFactory(new AlwaysAcceptConsensusBlockGate());
            using var bundle = InMemoryChainStoreBundle.Open();

            var node = factory.Build(
                bundle,
                new NoOpBlockSource(),
                new AlwaysContinuePolicy(),
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0));

            var forkConfig = ForkConfiguration.For(
                HardforkName.Prague,
                MainnetChainHardforkRegistry.Instance.Get(HardforkName.Prague),
                node.Config.DepositContractAddress);

            Assert.True(forkConfig.SystemContracts.TryGetValue("DEPOSIT_CONTRACT_ADDRESS", out var address));
            Assert.Equal("0x00000000219ab540356cbb839cbe05303d7705fa", address);
        }
    }
}
