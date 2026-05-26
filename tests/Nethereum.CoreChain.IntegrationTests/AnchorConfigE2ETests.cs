using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
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
    public class AnchorConfigE2ETests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public AnchorConfigE2ETests(ITestOutputHelper output) { _output = output; }

        private async Task<DevChainNode> ProduceL2Blocks(int count, InMemoryWitnessStore witnessStore = null,
            IBlockProver prover = null, ProofCadence cadence = null)
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            { ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false });
            if (witnessStore != null) node.WitnessStore = witnessStore;
            if (prover != null) node.BlockProver = prover;
            if (cadence != null) node.ProofCadence = cadence;
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < count; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }
            return node;
        }

        private async Task<(MockAnchorService anchors, List<AnchorPublicationResult> results)> AnchorToL1(
            DevChainNode l2, DevChainNode l1, int cadence, AnchorPublicationPipeline pipeline)
        {
            var anchorService = new MockAnchorService();
            var results = new List<AnchorPublicationResult>();
            var latestBlock = (long)await l2.GetBlockNumberAsync();
            ulong l1Nonce = 0;

            for (long b = cadence; b <= latestBlock; b += cadence)
            {
                var block = await l2.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = 31337,
                    Kind = cadence == 1 ? AnchorKind.Block : AnchorKind.Batch,
                    StartBlock = b - cadence + 1, EndBlock = b,
                    StateRoot = block.StateRoot,
                    TransactionsRoot = block.TransactionsHash,
                    ReceiptsRoot = block.ReceiptHash
                };

                var result = await pipeline.ExecuteAsync(scope);
                results.Add(result);

                var l1Tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), (BigInteger)1337,
                        "0x2222222222222222222222222222222222222222",
                        0, l1Nonce++, 1_000_000_000, 100_000,
                        result.EncodedPayload.ToHex()));
                var l1Result = await l1.SendTransactionAsync(l1Tx);
                await l1.MineBlockAsync();
                Assert.True(l1Result.Success, $"L1 anchor tx failed for L2 block {b}");

                await anchorService.AnchorBlockAsync(b, block.StateRoot,
                    block.TransactionsHash, block.ReceiptHash, result.EncodedPayload);

                if (result.EncodedPayload != null && l1Result.TransactionHash != null)
                    pipeline.RecordAnchorTx(b, l1Result.TransactionHash, result.EncodedPayload);
            }

            return (anchorService, results);
        }

        private async Task<DevChainNode> CreateL1()
        {
            var l1 = DevChainNode.CreateInMemory(new DevChainConfig
            { ChainId = 1337, BlockGasLimit = 30_000_000, AutoMine = false });
            await l1.StartAsync(new[] { _sender });
            return l1;
        }

        // ========================================
        // Config-named E2E tests
        // ========================================

        [Fact]
        public async Task NoAnchor_NoProof_NoCarrier_NoDA()
        {
            var l2 = await ProduceL2Blocks(10);
            Assert.Null(l2.WitnessStore);
            Assert.Equal(10, (long)await l2.GetBlockNumberAsync());
            _output.WriteLine("NoAnchor_NoProof_NoCarrier_NoDA: zero overhead — PASS");
            l2.Dispose();
        }

        [Fact]
        public async Task BatchRoot_NoProof_NoCarrier_NoDA()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2Blocks(10, ws);
            var l1 = await CreateL1();

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws);
            var (anchors, results) = await AnchorToL1(l2, l1, 5, pipeline);

            Assert.Equal(2, anchors.Anchors.Count);
            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                Assert.Equal((byte)AnchorKind.Batch, d.Header.AnchorKind);
                Assert.NotNull(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.StateRoot));
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof));
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa));
            }
            Assert.Equal(2, (long)await l1.GetBlockNumberAsync());
            _output.WriteLine("BatchRoot_NoProof_NoCarrier_NoDA: 2 batch anchors on L1 — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BlockRoot_NoProof_NoCarrier_NoDA()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2Blocks(5, ws);
            var l1 = await CreateL1();

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws);
            var (anchors, results) = await AnchorToL1(l2, l1, 1, pipeline);

            Assert.Equal(5, anchors.Anchors.Count);
            for (int b = 1; b <= 5; b++)
            {
                var block = await l2.GetBlockByNumberAsync(b);
                var d = AnchorPayloadCodec.Decode(anchors.Anchors[b].ExtraData);
                Assert.Equal((byte)AnchorKind.Block, d.Header.AnchorKind);
                var root = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.StateRoot);
                Assert.Equal(block.StateRoot, root.Bytes);
            }
            Assert.Equal(5, (long)await l1.GetBlockNumberAsync());
            _output.WriteLine("BlockRoot_NoProof_NoCarrier_NoDA: 5 block anchors, roots match — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BlockRoot_ProvesPerBlock_CarrierInline_DACalldata()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2Blocks(10, ws, new MockBlockProver(), ProofCadence.Continuous);
            var l1 = await CreateL1();

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws)
                .AddContributor(new InlineProofContributor(ws))
                .AddContributor(new CalldataDataContributor(ws));

            var (anchors, results) = await AnchorToL1(l2, l1, 1, pipeline);
            Assert.Equal(10, anchors.Anchors.Count);
            Assert.Equal(10, (long)await l1.GetBlockNumberAsync());

            bool anyProof = false, anyPointer = false;
            for (int b = 1; b <= 10; b++)
            {
                var d = AnchorPayloadCodec.Decode(anchors.Anchors[b].ExtraData);
                var da = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa);
                Assert.NotNull(da);

                var proof = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof);
                if (proof != null)
                {
                    anyProof = true;
                    var recovered = BlockProofSubmitter.DeserializeProofPayload(proof.Bytes);
                    Assert.Equal("Mock", recovered.ProverMode);
                }

                var ptr = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.PreviousValidatedPointer);
                if (ptr != null) anyPointer = true;

                _output.WriteLine($"Block {b}: DA={da.Bytes.Length}b, proof={proof != null}, pointer={ptr != null}");
            }

            Assert.True(anyProof, "Should have inline proofs");
            Assert.True(anyPointer, "Should advance validated pointer");

            var receipt = pipeline.ReceiptRecorder.GetReceipt(5);
            Assert.NotNull(receipt);
            Assert.True(receipt.SectionOffsets.ContainsKey(AnchorPayloadSectionType.InlineDa));

            _output.WriteLine("BlockRoot_ProvesPerBlock_CarrierInline_DACalldata: regulated settlement — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BatchRoot_ProvesOnDemand_NoCarrier_NoDA()
        {
            var ws = new InMemoryWitnessStore();
            var queue = new InMemoryProofRequestQueue();
            var l2 = await ProduceL2Blocks(10, ws, null, ProofCadence.Off);
            var l1 = await CreateL1();

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws, queue)
                .AddContributor(new InlineProofContributor(ws));

            var (anchors, results) = await AnchorToL1(l2, l1, 5, pipeline);
            Assert.Equal(2, anchors.Anchors.Count);

            var status5 = await queue.GetStatusAsync(5);
            Assert.NotNull(status5);
            _output.WriteLine($"Block 5 enqueued: {status5.Status}");

            var prover = new MockBlockProver();
            for (int b = 1; b <= 5; b++)
            {
                var w = await ws.GetWitnessAsync(b);
                if (w != null)
                {
                    var p = await prover.ProveBlockAsync(w, null, null, b);
                    await ws.StoreProofAsync(b, p);
                }
            }
            _output.WriteLine("On-demand proofs for blocks 1-5 generated");

            var pipeline2 = new AnchorPublicationPipeline(StateModel.MptKeccak, ws, queue);
            var block15 = await l2.GetBlockByNumberAsync(10);
            var scope = new AnchorScope
            {
                ChainId = 31337, Kind = AnchorKind.Batch,
                StartBlock = 11, EndBlock = 15, StateRoot = block15.StateRoot
            };
            var result = await pipeline2.ExecuteAsync(scope);
            var d = AnchorPayloadCodec.Decode(result.EncodedPayload);
            var ptr = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.PreviousValidatedPointer);
            Assert.NotNull(ptr);
            _output.WriteLine($"Next anchor: validatedThrough={BitConverter.ToInt64(ptr.Bytes)}");

            _output.WriteLine("BatchRoot_ProvesOnDemand_NoCarrier_NoDA: game economy on-demand — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BatchRoot_ProvesPerBatch_CarrierBlob_DABlob()
        {
            var ws = new InMemoryWitnessStore();
            var blobStore = new InMemoryBlobStore();
            var l2 = await ProduceL2Blocks(10, ws, new MockBlockProver(), ProofCadence.Continuous);
            var l1 = await CreateL1();

            var kzg = new MockBlobKzgProvider();
            var proofPublisher = new BlobProofPublisher(kzg, blobStore);
            var daPublisher = new BlobDataAvailabilityPublisher(kzg, blobStore);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws)
                .WithProofPublisher(proofPublisher)
                .WithDaPublisher(daPublisher);

            var (anchors, results) = await AnchorToL1(l2, l1, 5, pipeline);
            Assert.Equal(2, anchors.Anchors.Count);

            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                Assert.Equal((byte)AnchorKind.Batch, d.Header.AnchorKind);

                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof));
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa));

                var daCommit = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.DaCommitment);
                Assert.NotNull(daCommit);
                _output.WriteLine($"DA commitment: {daCommit.Bytes.Length} bytes");
            }

            var allBlobs = await blobStore.GetBlobsByBlockNumberAsync(5);
            Assert.NotEmpty(allBlobs);
            _output.WriteLine($"Blob store: {allBlobs.Count} blobs for batch ending at block 5");

            var blobData = new List<byte[]>();
            foreach (var b in allBlobs) blobData.Add(b.Blob);
            var decoded = BlobEncoder.DecodeBlobs(blobData);
            Assert.True(decoded.Length > 0);
            _output.WriteLine($"Decoded blob data: {decoded.Length} bytes");

            _output.WriteLine("BatchRoot_ProvesPerBatch_CarrierBlob_DABlob: DeFi rollup — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BlockRoot_ProvesPerBlock_CarrierBlob_DABlob()
        {
            var ws = new InMemoryWitnessStore();
            var blobStore = new InMemoryBlobStore();
            var l2 = await ProduceL2Blocks(5, ws, new MockBlockProver(), ProofCadence.Continuous);
            var l1 = await CreateL1();

            var kzg = new MockBlobKzgProvider();
            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws)
                .WithProofPublisher(new BlobProofPublisher(kzg, blobStore))
                .WithDaPublisher(new BlobDataAvailabilityPublisher(kzg, blobStore));

            var (anchors, results) = await AnchorToL1(l2, l1, 1, pipeline);
            Assert.Equal(5, anchors.Anchors.Count);
            Assert.Equal(5, (long)await l1.GetBlockNumberAsync());

            for (int b = 1; b <= 5; b++)
            {
                var d = AnchorPayloadCodec.Decode(anchors.Anchors[b].ExtraData);
                Assert.Equal((byte)AnchorKind.Block, d.Header.AnchorKind);
                var daCommit = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.DaCommitment);
                Assert.NotNull(daCommit);
            }

            _output.WriteLine("BlockRoot_ProvesPerBlock_CarrierBlob_DABlob: bridge hub — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task BatchRoot_ProvesOnDemand_CarrierBlob_DABlob()
        {
            var ws = new InMemoryWitnessStore();
            var blobStore = new InMemoryBlobStore();
            var queue = new InMemoryProofRequestQueue();
            var l2 = await ProduceL2Blocks(10, ws, null, ProofCadence.Off);
            var l1 = await CreateL1();

            var kzg = new MockBlobKzgProvider();
            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws, queue)
                .WithDaPublisher(new BlobDataAvailabilityPublisher(kzg, blobStore));

            var (anchors, results) = await AnchorToL1(l2, l1, 5, pipeline);

            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                var daCommit = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.DaCommitment);
                Assert.NotNull(daCommit);
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.ProofCommitment));
            }

            var prover = new MockBlockProver();
            for (int b = 1; b <= 5; b++)
            {
                var w = await ws.GetWitnessAsync(b);
                if (w != null)
                {
                    var p = await prover.ProveBlockAsync(w, null, null, b);
                    await ws.StoreProofAsync(b, p);
                }
            }
            _output.WriteLine("On-demand proofs for blocks 1-5");

            var proofPub = new BlobProofPublisher(kzg, blobStore);
            var pipeline2 = new AnchorPublicationPipeline(StateModel.MptKeccak, ws, queue)
                .WithProofPublisher(proofPub)
                .WithDaPublisher(new BlobDataAvailabilityPublisher(kzg, blobStore));

            var block10 = await l2.GetBlockByNumberAsync(10);
            var scope = new AnchorScope
            {
                ChainId = 31337, Kind = AnchorKind.Batch,
                StartBlock = 11, EndBlock = 15, StateRoot = block10.StateRoot
            };
            var nextResult = await pipeline2.ExecuteAsync(scope);
            var nd = AnchorPayloadCodec.Decode(nextResult.EncodedPayload);
            var ptr = AnchorPayloadCodec.FindSection(nd, AnchorPayloadSectionType.PreviousValidatedPointer);
            Assert.NotNull(ptr);
            _output.WriteLine($"After on-demand prove: validatedThrough={BitConverter.ToInt64(ptr.Bytes)}");

            _output.WriteLine("BatchRoot_ProvesOnDemand_CarrierBlob_DABlob: high-TPS game — PASS");
            l2.Dispose(); l1.Dispose();
        }
    }
}
