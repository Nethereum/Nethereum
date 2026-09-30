using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.EVM.Precompiles;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class FollowerChainNodeE2ETests : IDisposable
    {
        private readonly string _dataDir;

        public FollowerChainNodeE2ETests()
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

                await using var node = new FollowerChainNode(
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
                // AMS-7928-34: the follower retains EIP-7928 block access lists in the bundle, and until
                // the node surfaced the same store an RPC caller holding the node could not reach one.
                Assert.NotNull(bundle.BlockAccessLists);
                Assert.Same(bundle.BlockAccessLists, node.BlockAccessLists);
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
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
            var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            var node = new FollowerChainNode(
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

        [Fact]
        public async Task MainnetChainNode_WithSubmissionService_DelegatesTransactionSubmission()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
            var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            var node = new FollowerChainNode(
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

            var expectedHash = new byte[] { 0xab, 0xcd };
            node.TransactionSubmission = new StubSubmission(new TransactionExecutionResult
            {
                Success = true,
                TransactionHash = expectedHash,
            });

            var result = await node.SendTransactionAsync(null);

            Assert.True(result.Success);
            Assert.Same(expectedHash, result.TransactionHash);
        }

        private sealed class StubSubmission : ITransactionSubmissionService
        {
            private readonly TransactionExecutionResult _result;
            public StubSubmission(TransactionExecutionResult result) => _result = result;
            public Task<TransactionExecutionResult> SubmitAsync(
                ISignedTransaction transaction, CancellationToken cancellationToken = default)
                => Task.FromResult(_result);
        }

        private sealed class OptionsCapturingFollower : IFollowerService
        {
            public FollowerOptions Captured;

            public Task<FollowerRunResult> RunAsync(
                IBlockSource source,
                Func<IChainStoreBundle> bundleFactory,
                Func<IChainStoreBundle, IBlockExecutor> executorFactory,
                IValidationPolicy policy,
                ICanonicalStateRootSource canonical,
                FollowerOptions options,
                CancellationToken ct,
                ILogger logger = null)
            {
                Captured = options;
                return Task.FromResult(new FollowerRunResult(
                    FollowerExitReason.SourceCompleted, 0, 0, 0, 0, null, "captured"));
            }
        }

        [Fact]
        public async Task RunAsync_WithExplicitOptions_ForwardsCallerOptions_NotBaked()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            var capture = new OptionsCapturingFollower();
            var baked = new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0);
            var chainConfig = HiveTestdataFixture.ChainConfigFactory(HardforkName.Cancun);
            var hardforkConfig = HiveTestdataFixture.HardforkConfigFactory(HardforkName.Cancun);
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var txProcessor = new TransactionProcessor(
                bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig);

            var node = new FollowerChainNode(
                bundle: bundle,
                source: new HiveChainRlpBlockSource(HiveTestdataFixture.Chain),
                executorFactory: b => FollowerStackBuilder.Build(
                    b,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: new FixedPolicy(),
                options: baked,
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: txProcessor,
                txVerifier: txVerifier,
                follower: capture);

            await node.RunAsync(baked with { StartBlock = 999UL }, CancellationToken.None);
            Assert.Equal(999UL, capture.Captured.StartBlock);

            await node.RunAsync(CancellationToken.None);
            Assert.Equal(1UL, capture.Captured.StartBlock);
        }
    }
}
