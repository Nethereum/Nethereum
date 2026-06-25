using System;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.Strategies;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.Strategies
{
    [Collection(DevChainAnchorFixture.COLLECTION_NAME)]
    public class AnchoringStrategy_NoDA_NoProof_CommitmentOnly_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_NoDA_NoProof_CommitmentOnly_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitAndVerifyOnChain()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);
                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash,
                    b.ReceiptHash, h, new AnchorSubmissionPayload());
                Assert.Equal(AnchorStatus.Confirmed, r.Status);

                var onChain = await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId);
                Assert.Equal(5UL, onChain.EndBlock);
                Assert.Equal(h, onChain.EndBlockHash);
                Assert.Equal(b.StateRoot, onChain.PostStateRoot);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task ChainContinuity()
        {
            var appchain = await ProduceAppChain(10);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                for (int end = 5; end <= 10; end += 5)
                {
                    var b = await appchain.GetBlockByNumberAsync(end);
                    var h = await appchain.GetBlockHashByNumberAsync(end);
                    var r = await svc.AnchorBlockAsync(end, b.StateRoot, b.TransactionsHash,
                        b.ReceiptHash, h, new AnchorSubmissionPayload());
                    Assert.Equal(AnchorStatus.Confirmed, r.Status);
                }
                Assert.Equal(10UL, (await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId)).EndBlock);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task RestartRecovery()
        {
            var appchain = await ProduceAppChain(10);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);

                var s1 = CreateBatchService(chainId, genesis);
                await s1.InitializeAsync();
                var b5 = await appchain.GetBlockByNumberAsync(5);
                await s1.AnchorBlockAsync(5, b5.StateRoot, b5.TransactionsHash, b5.ReceiptHash,
                    await appchain.GetBlockHashByNumberAsync(5), new AnchorSubmissionPayload());

                var s2 = CreateBatchService(chainId, genesis);
                await s2.InitializeAsync();
                var b10 = await appchain.GetBlockByNumberAsync(10);
                var r2 = await s2.AnchorBlockAsync(10, b10.StateRoot, b10.TransactionsHash, b10.ReceiptHash,
                    await appchain.GetBlockHashByNumberAsync(10), new AnchorSubmissionPayload());
                Assert.Equal(AnchorStatus.Confirmed, r2.Status);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task RejectsGap()
        {
            var appchain = await ProduceAppChain(10);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();
                var b5 = await appchain.GetBlockByNumberAsync(5);
                await svc.AnchorBlockAsync(5, b5.StateRoot, b5.TransactionsHash, b5.ReceiptHash,
                    await appchain.GetBlockHashByNumberAsync(5), new AnchorSubmissionPayload());

                var b10 = await appchain.GetBlockByNumberAsync(10);
                var h10 = await appchain.GetBlockHashByNumberAsync(10);
                var bad = new AggregatedAnchor
                {
                    ChainId = chainId, GenesisHash = genesis, StartBlock = 8, EndBlock = 10,
                    AnchorVersion = 1, ProofSystem = 0, EndBlockHash = h10,
                    PreviousAnchorHash = await appchain.GetBlockHashByNumberAsync(5),
                    BlockHashesRoot = BlockHashesTree.ComputeRoot(new System.Collections.Generic.List<byte[]> { h10 }),
                    PostStateRoot = b10.StateRoot, ManifestHash = new byte[32]
                };
                await Assert.ThrowsAsync<Nethereum.ABI.FunctionEncoding.SmartContractRevertException>(() =>
                    Fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                        new SubmitAnchorFunction { A = bad, Proof = Array.Empty<byte>() }));
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task FullWorkerE2E()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                var strategy = new AnchoringStrategy_NoDA_NoProof_CommitmentOnly();
                var worker = new AnchorWorker(new L1NodeChainAnchorable(appchain), svc,
                    new AnchorConfig { Enabled = true, ChainId = chainId, AnchorCadence = 5,
                        AnchorIntervalMs = 60000, AnchorContractAddress = Fixture.AnchorService.ContractAddress },
                    strategy: strategy);
                await worker.StartAsync(default);
                await worker.ForceAnchorAsync(5);

                var onChain = await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId);
                Assert.Equal(5UL, onChain.EndBlock);
                Assert.Equal((await appchain.GetBlockByNumberAsync(5)).StateRoot, onChain.PostStateRoot);
                await worker.StopAsync(default);
            }
            finally { appchain.Dispose(); }
        }
    }
}
