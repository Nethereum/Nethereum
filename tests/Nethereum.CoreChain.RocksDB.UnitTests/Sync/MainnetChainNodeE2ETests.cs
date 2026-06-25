using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class MainnetChainNodeE2ETests : IDisposable
    {
        private readonly string _dataDir;

        public MainnetChainNodeE2ETests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"mainnet_chain_node_e2e_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public ValidationAction Verdict { get; set; } = ValidationAction.RewindAndRetry;
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => Verdict;
        }

        [Fact]
        public async Task MainnetChainNode_RunAsync_Drives_Follower_And_IChainNode_Surface_Works()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var bundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            try
            {
                await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);

                var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
                var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
                var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
                var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
                var txVerifier = new TransactionVerificationAndRecoveryImp();
                var txProcessor = new TransactionProcessor(
                    bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

                ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
                ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

                await using var node = new MainnetChainNode(
                    bundle: bundle,
                    source: source,
                    executorFactory: b => FollowerStackBuilder.Build(
                        b,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory),
                    policy: policy,
                    options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                    chainConfig: chainConfig,
                    hardforkConfig: hardforkConfig,
                    txProcessor: txProcessor,
                    txVerifier: txVerifier);

                var result = await node.RunAsync(CancellationToken.None);

                Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
                Assert.Equal(expectedBlocks, result.BlocksExecuted);
                Assert.Equal(0UL, result.RootMismatches);
                Assert.Equal(lastChainBlock, result.LastExecutedBlock);

                var observedHeight = await node.GetBlockNumberAsync();
                Assert.Equal((BigInteger)lastChainBlock, observedHeight);
                Assert.Same(bundle.Blocks, node.Blocks);
                Assert.Same(bundle.State, node.State);
                Assert.Same(chainConfig, node.Config);
            }
            catch
            {
                bundle.Dispose();
                throw;
            }
        }

        [Fact]
        public async Task MainnetChainNode_FollowerOnly_RejectsTransactionSubmission()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
            var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            var node = new MainnetChainNode(
                bundle: bundle,
                source: source,
                executorFactory: b => FollowerStackBuilder.Build(
                    b,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: new FixedPolicy(),
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier);

            var pending = await node.GetPendingTransactionsAsync();
            Assert.Empty(pending);

            var txResult = await node.SendTransactionAsync(null);
            Assert.False(txResult.Success);
            Assert.Contains("read-only", txResult.RevertReason);
        }
    }
}
