using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.Proving;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class AnchorE2ETests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public AnchorE2ETests(ITestOutputHelper output) { _output = output; }

        private async Task<DevChainNode> CreateL2(
            int blockCount,
            InMemoryWitnessStore witnessStore = null,
            IBlockProver prover = null,
            ProofCadence cadence = null)
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            if (witnessStore != null) node.WitnessStore = witnessStore;
            if (prover != null) node.BlockProver = prover;
            if (cadence != null) node.ProofCadence = cadence;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < blockCount; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }
            return node;
        }

        private async Task<(MockAnchorService anchors, AnchorPublicationPipeline pipeline)> RunAnchoring(
            DevChainNode node,
            int cadence,
            AnchorPublicationPipeline pipeline)
        {
            var anchorService = new MockAnchorService();
            var latestBlock = await node.GetBlockNumberAsync();

            for (long b = cadence; b <= (long)latestBlock; b += cadence)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = 31337,
                    Kind = cadence == 1 ? AnchorKind.Block : AnchorKind.Batch,
                    StartBlock = b - cadence + 1,
                    EndBlock = b,
                    StateRoot = block.StateRoot,
                    TransactionsRoot = block.TransactionsHash,
                    ReceiptsRoot = block.ReceiptHash
                };

                var result = await pipeline.ExecuteAsync(scope);
                var anchorInfo = await anchorService.AnchorBlockAsync(
                    b, block.StateRoot, block.TransactionsHash, block.ReceiptHash, result.EncodedPayload);

                if (anchorInfo.AnchorTxHash != null && result.EncodedPayload != null)
                    pipeline.RecordAnchorTx(b, anchorInfo.AnchorTxHash, result.EncodedPayload);
            }

            return (anchorService, pipeline);
        }

        [Fact]
        public async Task Scenario0_Centralised_NoAnchoring()
        {
            var node = await CreateL2(10);

            Assert.Null(node.WitnessStore);
            var latestBlock = await node.GetBlockNumberAsync();
            Assert.Equal(10, (long)latestBlock);

            _output.WriteLine("Scenario 0: 10 blocks, no witnesses, no proofs, no anchors — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario1_RootAnchoringOnly_PerBatch()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateL2(10, witnessStore);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore);
            var (anchors, _) = await RunAnchoring(node, 5, pipeline);

            Assert.Equal(2, anchors.Anchors.Count);
            Assert.True(anchors.Anchors.ContainsKey(5));
            Assert.True(anchors.Anchors.ContainsKey(10));

            foreach (var kvp in anchors.Anchors)
            {
                var decoded = AnchorPayloadCodec.Decode(kvp.Value.ExtraData);
                Assert.Equal(AnchorPayloadCodec.CurrentVersion, decoded.Header.Version);
                Assert.Equal((byte)StateModel.MptKeccak, decoded.Header.StateModel);
                Assert.Equal((byte)AnchorKind.Batch, decoded.Header.AnchorKind);

                Assert.NotNull(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot));
                Assert.Null(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof));
                Assert.Null(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa));

                _output.WriteLine($"Anchor block {kvp.Key}: {decoded.Sections.Count} sections, batch root only");
            }

            _output.WriteLine("Scenario 1: PerBatch(5), root anchoring only — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario2_PerBlockRootAnchoring()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateL2(5, witnessStore);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore);
            var (anchors, _) = await RunAnchoring(node, 1, pipeline);

            Assert.Equal(5, anchors.Anchors.Count);

            for (int b = 1; b <= 5; b++)
            {
                var anchor = anchors.Anchors[(BigInteger)b];
                var decoded = AnchorPayloadCodec.Decode(anchor.ExtraData);
                Assert.Equal((byte)AnchorKind.Block, decoded.Header.AnchorKind);

                var block = await node.GetBlockByNumberAsync(b);
                var rootSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
                Assert.Equal(block.StateRoot, rootSection.Bytes);

                _output.WriteLine($"Anchor block {b}: state root matches block header");
            }

            _output.WriteLine("Scenario 2: PerBlock, root anchoring only — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario3_SimpleL1Only_InlineProofCalldataDa()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateL2(10, witnessStore, new MockBlockProver(), ProofCadence.Continuous);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore)
                .AddContributor(new InlineProofContributor(witnessStore))
                .AddContributor(new CalldataDataContributor(witnessStore));

            var (anchors, _) = await RunAnchoring(node, 1, pipeline);
            Assert.Equal(10, anchors.Anchors.Count);

            var anchor1 = AnchorPayloadCodec.Decode(anchors.Anchors[1].ExtraData);
            var da1 = AnchorPayloadCodec.FindSection(anchor1, AnchorPayloadSectionType.InlineDa);
            Assert.NotNull(da1);
            Assert.True(da1.Bytes.Length > 100);
            _output.WriteLine($"Anchor block 1: DA section = {da1.Bytes.Length} bytes");

            var proof1 = AnchorPayloadCodec.FindSection(anchor1, AnchorPayloadSectionType.InlineProof);
            if (proof1 != null)
                _output.WriteLine($"Anchor block 1: proof available (inline prover, continuous cadence)");
            else
                _output.WriteLine("Anchor block 1: no proof yet (decoupled prover)");

            bool foundProof = proof1 != null;
            bool foundPointer = false;
            for (int b = 2; b <= 10; b++)
            {
                var decoded = AnchorPayloadCodec.Decode(anchors.Anchors[b].ExtraData);
                var proofSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
                var daSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa);
                var pointerSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.PreviousValidatedPointer);

                Assert.NotNull(daSection);

                if (proofSection != null)
                {
                    foundProof = true;
                    var proofPayload = Proving.BlockProofSubmitter.DeserializeProofPayload(proofSection.Bytes);
                    Assert.NotNull(proofPayload.ProofBytes);
                    Assert.Equal("Mock", proofPayload.ProverMode);
                    _output.WriteLine($"Anchor block {b}: InlineProof present, mode={proofPayload.ProverMode}");
                }

                if (pointerSection != null)
                {
                    foundPointer = true;
                    var validated = System.BitConverter.ToInt64(pointerSection.Bytes);
                    _output.WriteLine($"Anchor block {b}: PreviousValidatedPointer = {validated}");
                }
            }

            Assert.True(foundProof, "At least one anchor should carry a proof (two-phase)");
            Assert.True(foundPointer, "Proof pointer should advance at least once");

            var receipt5 = pipeline.ReceiptRecorder.GetReceipt(5);
            Assert.NotNull(receipt5);
            Assert.True(receipt5.SectionOffsets.ContainsKey(AnchorPayloadSectionType.InlineDa));
            _output.WriteLine($"Receipt block 5: DA offset={receipt5.SectionOffsets[AnchorPayloadSectionType.InlineDa].Offset}");

            _output.WriteLine("Scenario 3: PerBlock, InlineProof + CalldataDa, two-phase — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario_ProofQueueIntegration()
        {
            var witnessStore = new InMemoryWitnessStore();
            var proofQueue = new InMemoryProofRequestQueue();
            var node = await CreateL2(10, witnessStore, null, ProofCadence.Off);

            var pipeline = new AnchorPublicationPipeline(
                StateModel.MptKeccak, witnessStore, proofQueue);

            var (anchors, _) = await RunAnchoring(node, 5, pipeline);

            var status5 = await proofQueue.GetStatusAsync(5);
            var status10 = await proofQueue.GetStatusAsync(10);
            Assert.NotNull(status5);
            Assert.NotNull(status10);
            _output.WriteLine($"Block 5 enqueued: status={status5.Status}");
            _output.WriteLine($"Block 10 enqueued: status={status10.Status}");

            var prover = new MockBlockProver();
            for (int b = 1; b <= 5; b++)
            {
                var witness = await witnessStore.GetWitnessAsync(b);
                if (witness != null)
                {
                    var proof = await prover.ProveBlockAsync(witness, null, null, b);
                    await witnessStore.StoreProofAsync(b, proof);
                }
            }

            var pipeline2 = new AnchorPublicationPipeline(
                StateModel.MptKeccak, witnessStore, proofQueue);

            var block15Block = await node.GetBlockByNumberAsync(10);
            var scope = new AnchorScope
            {
                ChainId = 31337, Kind = AnchorKind.Batch,
                StartBlock = 11, EndBlock = 15,
                StateRoot = block15Block.StateRoot
            };
            var result = await pipeline2.ExecuteAsync(scope);
            var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);
            var pointer = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.PreviousValidatedPointer);

            Assert.NotNull(pointer);
            var validated = System.BitConverter.ToInt64(pointer.Bytes);
            Assert.True(validated >= 1, $"Pointer should advance, got {validated}");
            _output.WriteLine($"After proving blocks 1-5: pointer advanced to {validated}");

            _output.WriteLine("Scenario: Proof queue integration — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario_FailedProofDoesntBlockAnchoring()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateL2(5, witnessStore);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore)
                .AddContributor(new InlineProofContributor(witnessStore));

            var (anchors, _) = await RunAnchoring(node, 1, pipeline);
            Assert.Equal(5, anchors.Anchors.Count);

            for (int b = 1; b <= 5; b++)
            {
                var decoded = AnchorPayloadCodec.Decode(anchors.Anchors[b].ExtraData);
                var root = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
                Assert.NotNull(root);

                var proof = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
                Assert.Null(proof);
            }

            _output.WriteLine("Scenario: No prover configured, anchors still go out with state roots — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario_RetentionAfterProving()
        {
            BigInteger chainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = ProofCadence.Continuous;
            node.WitnessRetention = WitnessRetentionPolicy.UntilProven;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 5; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            for (int b = 1; b <= 5; b++)
            {
                var proof = await witnessStore.GetProofAsync(b);
                Assert.NotNull(proof);
                var witness = await witnessStore.GetWitnessAsync(b);
                Assert.Null(witness);
                _output.WriteLine($"Block {b}: proof stored, witness purged");
            }

            _output.WriteLine("Scenario: UntilProven retention works through inline proving — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Scenario_Upgrade2To3_AddProofsToRootOnlyChain()
        {
            var witnessStore = new InMemoryWitnessStore();
            var node = await CreateL2(5, witnessStore, null, ProofCadence.Off);

            var pipeline1 = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore);
            var anchorService = new MockAnchorService();

            for (int b = 1; b <= 5; b++)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = 31337, Kind = AnchorKind.Block,
                    StartBlock = b, EndBlock = b,
                    StateRoot = block.StateRoot
                };
                var result = await pipeline1.ExecuteAsync(scope);
                await anchorService.AnchorBlockAsync(b, block.StateRoot, null, null, result.EncodedPayload);
            }

            for (int b = 1; b <= 5; b++)
            {
                var decoded = AnchorPayloadCodec.Decode(anchorService.Anchors[b].ExtraData);
                Assert.Null(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof));
            }
            _output.WriteLine("Phase 1: Blocks 1-5 anchored without proofs");

            node.BlockProver = new MockBlockProver();
            node.ProofCadence = ProofCadence.Continuous;

            ulong nonce = 5;
            for (int b = 0; b < 5; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), (BigInteger)31337,
                        $"0x{(b + 100):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            var pipeline2 = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore)
                .AddContributor(new InlineProofContributor(witnessStore));

            for (int b = 6; b <= 10; b++)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = 31337, Kind = AnchorKind.Block,
                    StartBlock = b, EndBlock = b,
                    StateRoot = block.StateRoot
                };
                var result = await pipeline2.ExecuteAsync(scope);
                await anchorService.AnchorBlockAsync(b, block.StateRoot, null, null, result.EncodedPayload);
            }

            bool anyProof = false;
            for (int b = 6; b <= 10; b++)
            {
                var decoded = AnchorPayloadCodec.Decode(anchorService.Anchors[b].ExtraData);
                var proofSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
                if (proofSection != null) anyProof = true;
            }
            Assert.True(anyProof, "Blocks 6-10 should have proof sections after upgrade");
            _output.WriteLine("Phase 2: Blocks 6-10 anchored WITH proofs — upgrade without migration");

            _output.WriteLine("Scenario: 2→3 upgrade — PASS");
            node.Dispose();
        }

        [Fact]
        public void Scenario_ConfigValidation_AllTiers()
        {
            var tier0 = new AnchorPublicationConfig { AnchorGranularity = 0, ProofMode = ProofCadenceMode.Off, DaMode = DaMode.None };
            Assert.Empty(AnchorConfigValidator.Validate(tier0));

            var tier1 = new AnchorPublicationConfig { AnchorGranularity = 100, ProofMode = ProofCadenceMode.Off, DaMode = DaMode.None };
            Assert.Empty(AnchorConfigValidator.Validate(tier1));

            var tier2 = new AnchorPublicationConfig { AnchorGranularity = 1, ProofMode = ProofCadenceMode.Off, DaMode = DaMode.None };
            Assert.Empty(AnchorConfigValidator.Validate(tier2));

            var tier3 = new AnchorPublicationConfig { AnchorGranularity = 1, ProofMode = ProofCadenceMode.Continuous, ProofCarrier = ProofCarrierMode.Inline, DaMode = DaMode.Federated };
            Assert.Empty(AnchorConfigValidator.Validate(tier3));

            var tier4 = new AnchorPublicationConfig { AnchorGranularity = 100, ProofMode = ProofCadenceMode.Periodic, ProofCarrier = ProofCarrierMode.Blob, DaMode = DaMode.Public };
            Assert.Empty(AnchorConfigValidator.Validate(tier4));

            var tier5 = new AnchorPublicationConfig { AnchorGranularity = 100, ProofMode = ProofCadenceMode.Periodic, ProofCarrier = ProofCarrierMode.Blob, DaMode = DaMode.Public };
            Assert.Empty(AnchorConfigValidator.Validate(tier5));

            _output.WriteLine("All 6 tier configs valid");

            Assert.NotEmpty(AnchorConfigValidator.Validate(new AnchorPublicationConfig { ProofMode = ProofCadenceMode.Off, ProofCarrier = ProofCarrierMode.Blob }));
            Assert.NotEmpty(AnchorConfigValidator.Validate(new AnchorPublicationConfig { ProofMode = ProofCadenceMode.Continuous }));
            // DaMode.Public with ProofMode.Off is valid (Stage 2)
            Assert.Empty(AnchorConfigValidator.Validate(new AnchorPublicationConfig { DaMode = DaMode.Public }));

            _output.WriteLine("Invalid combos rejected");
            _output.WriteLine("Scenario: Config validation all tiers — PASS");
        }
    }
}
