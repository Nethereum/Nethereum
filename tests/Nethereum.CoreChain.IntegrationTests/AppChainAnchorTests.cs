using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Proving;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    [Collection(DevChainAnchorFixture.COLLECTION_NAME)]
    public class AppChainAnchorTests
    {
        private readonly DevChainAnchorFixture _fixture;
        private readonly ITestOutputHelper _output;
        private static int _testCounter;

        public AppChainAnchorTests(DevChainAnchorFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private static AggregatedAnchor CloneAnchor(AggregatedAnchor a) => new()
        {
            ChainId = a.ChainId, GenesisHash = a.GenesisHash,
            StartBlock = a.StartBlock, EndBlock = a.EndBlock,
            AnchorVersion = a.AnchorVersion, ProofSystem = a.ProofSystem,
            EndBlockHash = a.EndBlockHash, PreviousAnchorHash = a.PreviousAnchorHash,
            BlockHashesRoot = a.BlockHashesRoot, PostStateRoot = a.PostStateRoot,
            ManifestHash = a.ManifestHash
        };

        private async Task<(ulong chainId, byte[] genesisHash)> RegisterChain(
            byte[] genesisStateRoot, byte minimumProofSystem = 0)
        {
            var id = Interlocked.Increment(ref _testCounter);
            var genesisHash = new Nethereum.Util.Sha3Keccack()
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes($"appchain-{id}-{DateTime.UtcNow.Ticks}"));
            var chainId = (ulong)(30000 + id);

            await _fixture.AnchorService.RegisterAppChainRequestAndWaitForReceiptAsync(
                new RegisterAppChainFunction
                {
                    ChainId = chainId, GenesisHash = genesisHash, GenesisBlock = 1,
                    GenesisStateRoot = genesisStateRoot,
                    MinimumProofSystem = minimumProofSystem, MinimumAnchorVersion = 1,
                    Authority = _fixture.AuthorityService.ContractAddress
                });
            await _fixture.AuthorityService.SetOperatorRequestAndWaitForReceiptAsync(
                new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.SetOperatorFunction
                { ChainId = chainId, NewOperator = _fixture.OperatorAccount.Address });
            return (chainId, genesisHash);
        }

        private async Task VerifyOnChain(ulong chainId, DevChainNode l2, ulong endBlock, byte[] manifestHash)
        {
            var s = await _fixture.AnchorService.GetLatestAnchorQueryAsync(chainId);
            Assert.Equal(endBlock, s.EndBlock);
            Assert.Equal(await l2.GetBlockHashByNumberAsync(endBlock), s.EndBlockHash);
            Assert.Equal((await l2.GetBlockByNumberAsync(endBlock)).StateRoot, s.PostStateRoot);
            Assert.Equal(manifestHash, s.ManifestHash);
            _output.WriteLine($"  On-chain: endBlock={s.EndBlock}, all 4 fields match L2");
        }

        private async Task<DevChainNode> ProduceL2(int blocks, InMemoryWitnessStore ws = null,
            IBlockProver prover = null, ProofCadence cadence = null)
        {
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            { ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = false });
            if (ws != null) l2.WitnessStore = ws;
            if (prover != null) l2.BlockProver = prover;
            if (cadence != null) l2.ProofCadence = cadence;
            await l2.StartAsync(new[] { _fixture.OperatorAccount.Address });

            var signer = new Nethereum.Signer.LegacyTransactionSigner();
            var pkBytes = _fixture.OperatorPrivateKey.Substring(2).HexToByteArray();
            ulong n = 0;
            for (int b = 0; b < blocks; b++)
            {
                var txHex = signer.SignTransaction(pkBytes, (System.Numerics.BigInteger)31337,
                    $"0x{(b + 1):x40}", 1000, n++, 1_000_000_000, 21_000, "");
                await l2.SendTransactionAsync(Nethereum.Model.TransactionFactory.CreateTransaction(txHex));
                await l2.MineBlockAsync();
            }
            return l2;
        }

        // ═══════════════════════════════════════════
        //  ANCHOR ONLY
        // ═══════════════════════════════════════════

        [Fact]
        public async Task AnchorOnly_SubmitTwoBatches_AllFieldsVerifiedOnChain()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);

                var b1 = await builder.BuildAsync(l2, 1, 5, ws);
                var r1 = await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(b1);
                Assert.Equal(1, r1.Status.Value);
                await VerifyOnChain(chainId, l2, 5, b1.Anchor.ManifestHash);

                Assert.NotNull(b1.BlockHashesTree);
                Assert.Equal(5, b1.BlockLeaves.Count);
                _output.WriteLine($"Batch 1-5: {b1.BlockLeaves.Count} rich leaves, gas={r1.GasUsed}");

                var b2 = await builder.BuildAsync(l2, 6, 10, ws);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(b2);
                await VerifyOnChain(chainId, l2, 10, b2.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task AnchorOnly_ManifestHashStoredOnChain_RoundTrips()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build = await builder.BuildAsync(l2, 1, 5, ws);

                Assert.Equal(build.Anchor.ManifestHash, build.Manifest.ComputeManifestHash());
                var rt = BatchManifest.Deserialize(build.Manifest.Serialize());
                Assert.Equal(build.Anchor.ManifestHash, rt.ComputeManifestHash());

                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build);
                var s = await _fixture.AnchorService.GetLatestAnchorQueryAsync(chainId);
                Assert.Equal(build.Anchor.ManifestHash, s.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task AnchorOnly_RejectGapInBlockRange()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);
                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                var gap = new AnchorBuilder(chainId, genesis, 1, 0,
                    builder.PreviousAnchorHash, builder.PreviousPostStateRoot);
                var gapBuild = await gap.BuildAsync(l2, 7, 10, ws);
                var gapAnchor = CloneAnchor(gapBuild.Anchor);
                gapAnchor.StartBlock = 7;

                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                        new SubmitAnchorFunction { A = gapAnchor, Proof = Array.Empty<byte>() }));
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task AnchorOnly_RejectBrokenPreviousAnchorHash()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);
                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                var b2Builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    builder.PreviousAnchorHash, builder.PreviousPostStateRoot);
                var build2 = await b2Builder.BuildAsync(l2, 6, 10, ws);
                var broken = CloneAnchor(build2.Anchor);
                broken.PreviousAnchorHash = new byte[32];

                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                        new SubmitAnchorFunction { A = broken, Proof = Array.Empty<byte>() }));

                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build2);
                await VerifyOnChain(chainId, l2, 10, build2.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  ACCESS CONTROL
        // ═══════════════════════════════════════════

        [Fact]
        public async Task AccessControl_RejectNonOperatorSubmission()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);
                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build = await builder.BuildAsync(l2, 1, 5, ws);

                var challengerService = _fixture.CreateAnchorServiceAs(_fixture.ChallengerWeb3);
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => challengerService.SubmitAnchorRequestAndWaitForReceiptAsync(build));

                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task AccessControl_OperatorTransfer_NewOperatorCanSubmit()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);
                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                await _fixture.AuthorityService.SetOperatorRequestAndWaitForReceiptAsync(
                    new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.SetOperatorFunction
                    { ChainId = chainId, NewOperator = _fixture.ChallengerAccount.Address });

                var b2 = await builder.BuildAsync(l2, 6, 10, ws);
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(b2));

                var newService = _fixture.CreateAnchorServiceAs(_fixture.ChallengerWeb3);
                await newService.SubmitAnchorRequestAndWaitForReceiptAsync(b2);
                await VerifyOnChain(chainId, l2, 10, b2.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  FULL ZK
        // ═══════════════════════════════════════════

        [Fact]
        public async Task FullZK_ProvenBatches_AllFieldsVerifiedOnChain()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws, new MockBlockProver(), ProofCadence.Continuous);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot, minimumProofSystem: 2);
                var builder = new AnchorBuilder(chainId, genesis, 1, 2,
                    initialPreviousPostStateRoot: block1.StateRoot);

                var b1 = await builder.BuildAsync(l2, 1, 5, ws);
                Assert.Equal(MockBlockProver.Groth16ProofSize, b1.ProofBytes.Length);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(b1);
                await VerifyOnChain(chainId, l2, 5, b1.Anchor.ManifestHash);

                var b2 = await builder.BuildAsync(l2, 6, 10, ws);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(b2);
                await VerifyOnChain(chainId, l2, 10, b2.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task FullZK_RejectUnprovenSubmission()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws, new MockBlockProver(), ProofCadence.Continuous);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot, minimumProofSystem: 2);
                var builder = new AnchorBuilder(chainId, genesis, 1, 2,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build = await builder.BuildAsync(l2, 1, 5, ws);

                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                        new SubmitAnchorFunction { A = CloneAnchor(build.Anchor), Proof = Array.Empty<byte>() }));

                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build);
                await VerifyOnChain(chainId, l2, 5, build.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task FullZK_RejectMalformedProofs()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws, new MockBlockProver(), ProofCadence.Continuous);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot, minimumProofSystem: 2);
                var builder = new AnchorBuilder(chainId, genesis, 1, 2,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build = await builder.BuildAsync(l2, 1, 5, ws);
                var anchor = CloneAnchor(build.Anchor);

                foreach (var (label, proof) in new[] {
                    ("empty", Array.Empty<byte>()), ("32-byte", new byte[32]), ("256-byte zero", new byte[256]) })
                {
                    await Assert.ThrowsAsync<SmartContractRevertException>(
                        () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                            new SubmitAnchorFunction { A = anchor, Proof = proof }));
                }

                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build);
                await VerifyOnChain(chainId, l2, 5, build.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  GRADUATION
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Graduation_RaiseMinimum_UnprovenRejectedProvenAccepted()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(20, ws, new MockBlockProver(), ProofCadence.Continuous);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot, minimumProofSystem: 0);

                var b0 = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build1 = await b0.BuildAsync(l2, 1, 10, ws);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build1);
                await VerifyOnChain(chainId, l2, 10, build1.Anchor.ManifestHash);

                await _fixture.AnchorService.RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(
                    new RaiseMinimumProofSystemFunction { ChainId = chainId, NewFloor = 2 });

                var ub = new AnchorBuilder(chainId, genesis, 1, 0,
                    b0.PreviousAnchorHash, b0.PreviousPostStateRoot);
                var ubBuild = await ub.BuildAsync(l2, 11, 20, ws);
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(ubBuild));

                var pb = new AnchorBuilder(chainId, genesis, 1, 2,
                    b0.PreviousAnchorHash, b0.PreviousPostStateRoot);
                var pbBuild = await pb.BuildAsync(l2, 11, 20, ws);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(pbBuild);
                await VerifyOnChain(chainId, l2, 20, pbBuild.Anchor.ManifestHash);
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task Graduation_RejectLoweringMinimum()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, _) = await RegisterChain(block1.StateRoot, minimumProofSystem: 2);

                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(
                        new RaiseMinimumProofSystemFunction { ChainId = chainId, NewFloor = 0 }));
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.AnchorService.RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(
                        new RaiseMinimumProofSystemFunction { ChainId = chainId, NewFloor = 2 }));
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  GAS BENCHMARK
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Benchmark_GasPerConfiguration()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws, new MockBlockProver(), ProofCadence.Continuous);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var b0 = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                var build1 = await b0.BuildAsync(l2, 1, 5, ws);
                var r1 = await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build1);
                _output.WriteLine($"Anchor Only:  {r1.GasUsed} gas");

                await _fixture.AnchorService.RaiseMinimumProofSystemRequestAndWaitForReceiptAsync(
                    new RaiseMinimumProofSystemFunction { ChainId = chainId, NewFloor = 2 });

                var b1 = new AnchorBuilder(chainId, genesis, 1, 2,
                    b0.PreviousAnchorHash, b0.PreviousPostStateRoot);
                var build2 = await b1.BuildAsync(l2, 6, 10, ws);
                var r2 = await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(build2);
                _output.WriteLine($"Full ZK:      {r2.GasUsed} gas");
                _output.WriteLine($"ZK overhead:  {r2.GasUsed.Value - r1.GasUsed.Value} gas");
            }
            finally { l2.Dispose(); }
        }
    }
}
