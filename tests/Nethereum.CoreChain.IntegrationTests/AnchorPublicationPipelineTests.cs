using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.Proving;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class AnchorPublicationPipelineTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public AnchorPublicationPipelineTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task Tier3_PerBlockInlineProofCalldataDa()
        {
            BigInteger chainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var proofQueue = new InMemoryProofRequestQueue();
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = ProofCadence.Continuous;
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

            var pipeline = new AnchorPublicationPipeline(
                    StateModel.MptKeccak, witnessStore, proofQueue)
                .AddContributor(new InlineProofContributor(witnessStore))
                .AddContributor(new CalldataDataContributor(witnessStore));

            for (int b = 1; b <= 5; b++)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)chainId,
                    Kind = AnchorKind.Block,
                    StartBlock = b,
                    EndBlock = b,
                    StateRoot = block.StateRoot,
                    TransactionsRoot = block.TransactionsHash,
                    ReceiptsRoot = block.ReceiptHash
                };

                var result = await pipeline.ExecuteAsync(scope);
                Assert.NotNull(result.EncodedPayload);

                var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);
                Assert.Equal(AnchorPayloadCodec.CurrentVersion, decoded.Header.Version);
                Assert.Equal((byte)StateModel.MptKeccak, decoded.Header.StateModel);
                Assert.Equal((byte)AnchorKind.Block, decoded.Header.AnchorKind);

                var stateRootSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
                Assert.NotNull(stateRootSection);
                Assert.Equal(block.StateRoot, stateRootSection.Bytes);

                var proofSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
                Assert.NotNull(proofSection);
                Assert.True(proofSection.Bytes.Length > 0);

                var daSection = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa);
                Assert.NotNull(daSection);
                Assert.True(daSection.Bytes.Length > 0);

                Assert.Null(result.ProofPublication);
                Assert.Null(result.DaPublication);

                var txHash = new byte[32];
                txHash[0] = (byte)b;
                pipeline.RecordAnchorTx(b, txHash, result.EncodedPayload);

                _output.WriteLine($"Block {b}: payload={result.EncodedPayload.Length}b, " +
                    $"sections={decoded.Sections.Count}, " +
                    $"proof={proofSection.Bytes.Length}b, da={daSection.Bytes.Length}b" +
                    (result.PreviousValidatedBlock.HasValue ? $", validated={result.PreviousValidatedBlock}" : ""));
            }

            var receipt = pipeline.ReceiptRecorder.GetReceipt(3);
            Assert.NotNull(receipt);
            Assert.True(receipt.SectionOffsets.ContainsKey(AnchorPayloadSectionType.InlineProof));
            Assert.True(receipt.SectionOffsets[AnchorPayloadSectionType.InlineProof].Offset > 0);
            _output.WriteLine($"Receipt block 3: proof offset={receipt.SectionOffsets[AnchorPayloadSectionType.InlineProof].Offset}");

            var queueStatus = await proofQueue.GetStatusAsync(5);
            Assert.NotNull(queueStatus);
            _output.WriteLine($"Block 5 enqueued for proving: status={queueStatus.Status}");

            _output.WriteLine("Tier 3 E2E: per-block + inline proof + calldata DA — PASS");
            node.Dispose();
        }

        [Fact]
        public async Task Tier3_TwoPhaseProofAdvancement()
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

            var pipeline = new AnchorPublicationPipeline(
                    StateModel.MptKeccak, witnessStore)
                .AddContributor(new InlineProofContributor(witnessStore));

            long? lastValidated = null;

            for (int b = 1; b <= 5; b++)
            {
                var block = await node.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)chainId,
                    Kind = AnchorKind.Block,
                    StartBlock = b,
                    EndBlock = b,
                    StateRoot = block.StateRoot
                };

                var result = await pipeline.ExecuteAsync(scope);

                if (result.PreviousValidatedBlock.HasValue)
                {
                    lastValidated = result.PreviousValidatedBlock.Value;
                    _output.WriteLine($"Anchor block {b}: validatedThrough advanced to {lastValidated}");
                }
                else
                {
                    _output.WriteLine($"Anchor block {b}: no proof pointer advancement yet");
                }
            }

            Assert.NotNull(lastValidated);
            Assert.True(lastValidated >= 1, "Should have advanced validated pointer");
            _output.WriteLine($"Final validatedThrough: {lastValidated}");
            _output.WriteLine("Two-phase proof advancement: PASS");

            node.Dispose();
        }

        [Fact]
        public void ShouldRejectInvalidConfigCombinations()
        {
            var invalid1 = new AnchorPublicationConfig
            {
                ProofMode = ProofCadenceMode.Off,
                ProofCarrier = ProofCarrierMode.Blob
            };
            var errors1 = AnchorConfigValidator.Validate(invalid1);
            Assert.NotEmpty(errors1);
            _output.WriteLine($"Off + Blob: {errors1[0]}");

            var invalid2 = new AnchorPublicationConfig
            {
                ProofMode = ProofCadenceMode.Continuous,
                ProofCarrier = null
            };
            var errors2 = AnchorConfigValidator.Validate(invalid2);
            Assert.NotEmpty(errors2);
            _output.WriteLine($"Continuous + null: {errors2[0]}");

            var invalid3 = new AnchorPublicationConfig
            {
                ProofMode = ProofCadenceMode.Continuous,
                DaMode = DaMode.None
            };
            var errors3 = AnchorConfigValidator.Validate(invalid3);
            Assert.NotEmpty(errors3);
            _output.WriteLine($"Continuous + None DA: {errors3[0]}");

            var valid = new AnchorPublicationConfig
            {
                AnchorGranularity = 1,
                ProofMode = ProofCadenceMode.Continuous,
                ProofCarrier = ProofCarrierMode.Inline,
                DaMode = DaMode.Federated
            };
            var errorsValid = AnchorConfigValidator.Validate(valid);
            Assert.Empty(errorsValid);
            _output.WriteLine("Continuous + Federated DA: VALID");
        }

        [Fact]
        public async Task Tier1_RootAnchoringOnly()
        {
            BigInteger chainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = witnessStore;
            node.ProofCadence = ProofCadence.Off;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 3; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore);

            var block = await node.GetBlockByNumberAsync(3);
            var scope = new AnchorScope
            {
                ChainId = (long)chainId,
                Kind = AnchorKind.Batch,
                StartBlock = 1,
                EndBlock = 3,
                StateRoot = block.StateRoot
            };

            var result = await pipeline.ExecuteAsync(scope);
            var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);

            Assert.Equal((byte)AnchorKind.Batch, decoded.Header.AnchorKind);
            var stateRoot = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
            Assert.NotNull(stateRoot);
            Assert.Null(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof));
            Assert.Null(AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa));

            _output.WriteLine($"Tier 1: batch anchor blocks 1-3, payload={result.EncodedPayload.Length}b, state root only");
            _output.WriteLine("Tier 1 root anchoring only: PASS");

            node.Dispose();
        }
    }
}
