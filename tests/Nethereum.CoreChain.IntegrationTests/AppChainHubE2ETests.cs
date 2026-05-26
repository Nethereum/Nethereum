using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.Contracts;
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
    public class AppChainHubE2ETests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();
        private readonly FunctionCallEncoder _encoder = new();

        public AppChainHubE2ETests(ITestOutputHelper output) { _output = output; }

        private async Task<(DevChainNode l1, string hubAddress, ulong nextNonce)> DeployAndRegisterHub()
        {
            BigInteger l1ChainId = 1337;
            var l1 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = l1ChainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await l1.StartAsync(new[] { _sender });

            var constructorParams = _encoder.EncodeParameters(
                new[] {
                    new Nethereum.ABI.Model.Parameter("uint256", 1),
                    new Nethereum.ABI.Model.Parameter("uint256", 2),
                    new Nethereum.ABI.Model.Parameter("uint256", 3)
                },
                new object[] { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero });

            var fullBytecode = AppChainHubDeploymentBase.BYTECODE + constructorParams.ToHex();
            var deployTx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), l1ChainId,
                    "", 0, 0, 1_000_000_000, 5_000_000, fullBytecode));
            await l1.SendTransactionAsync(deployTx);
            await l1.MineBlockAsync();

            var hubAddress = Nethereum.Util.ContractUtils.CalculateContractAddress(_sender, 0);

            var sig = SignRegistration(31337, _sender);
            var regResult = await SendContractTx(l1, hubAddress,
                new RegisterAppChainFunction
                {
                    ChainId = 31337, Sequencer = _sender, SequencerSignature = sig
                }.GetCallData(), 1337, 1);
            Assert.True(regResult.Success, $"Registration failed: {regResult.RevertReason}");

            _output.WriteLine($"Hub at {hubAddress}, AppChain 31337 registered");
            return (l1, hubAddress, 2);
        }

        private async Task<DevChainNode> ProduceL2(int blocks, InMemoryWitnessStore ws,
            IBlockProver prover = null, ProofCadence cadence = null)
        {
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = false
            });
            l2.WitnessStore = ws;
            if (prover != null) l2.BlockProver = prover;
            if (cadence != null) l2.ProofCadence = cadence;
            await l2.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < blocks; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), (BigInteger)31337,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await l2.SendTransactionAsync(tx);
                await l2.MineBlockAsync();
            }
            return l2;
        }

        private byte[] SignRegistration(ulong chainId, string ownerAddress)
        {
            var packed = new byte[28];
            for (int i = 0; i < 8; i++)
                packed[7 - i] = (byte)(chainId >> (i * 8));
            Array.Copy(ownerAddress.HexToByteArray(), 0, packed, 8, 20);

            var keccak = new Nethereum.Util.Sha3Keccack();
            var msgHash = keccak.CalculateHash(packed);
            var msgSigner = new EthereumMessageSigner();
            var sig = msgSigner.Sign(msgHash, new EthECKey(_pk));
            return (sig.StartsWith("0x") ? sig.Substring(2) : sig).HexToByteArray();
        }

        private async Task<TransactionExecutionResult> SendContractTx(
            DevChainNode node, string contract, byte[] calldata,
            BigInteger chainId, ulong nonce, BigInteger value = default)
        {
            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    contract, value, nonce, 1_000_000_000, 2_000_000,
                    calldata.ToHex()));
            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            var blockResult = node.BlockManager.LastBlockProductionResult;
            if (blockResult?.TransactionResults?.Count > 0)
            {
                var r = blockResult.TransactionResults[0];
                return new TransactionExecutionResult
                {
                    TransactionHash = r.TxHash,
                    Success = r.Success,
                    GasUsed = r.GasUsed,
                    RevertReason = r.ErrorMessage,
                    ReturnData = r.ReturnData
                };
            }
            return new TransactionExecutionResult { Success = false, RevertReason = "No tx in block" };
        }

        private async Task<bool> VerifyAnchorOnChain(DevChainNode l1, string hubAddress,
            ulong chainId, ulong blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot)
        {
            var calldata = new VerifyAnchorFunction
            {
                ChainId = chainId, BlockNumber = blockNumber,
                StateRoot = stateRoot, TxRoot = txRoot, ReceiptRoot = receiptRoot
            }.GetCallData();
            var result = await l1.CallAsync(hubAddress, calldata);
            return result.Success && result.ReturnData?.Length >= 32 && result.ReturnData[31] == 1;
        }

        private async Task<(List<AnchorPublicationResult> results, long totalGas)> AnchorToHub(
            DevChainNode l2, DevChainNode l1, string hubAddress,
            int cadence, AnchorPublicationPipeline pipeline, ulong startNonce)
        {
            var results = new List<AnchorPublicationResult>();
            long totalGas = 0;
            var latestBlock = (long)await l2.GetBlockNumberAsync();
            ulong nonce = startNonce;

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

                var pubResult = await pipeline.ExecuteAsync(scope);
                results.Add(pubResult);

                var anchorCalldata = new AnchorFunction
                {
                    ChainId = 31337, BlockNumber = (ulong)b,
                    StateRoot = block.StateRoot,
                    TxRoot = block.TransactionsHash,
                    ReceiptRoot = block.ReceiptHash,
                    ProcessedUpToMessageId = 0,
                    ExtraData = pubResult.EncodedPayload
                }.GetCallData();

                var txResult = await SendContractTx(l1, hubAddress, anchorCalldata, 1337, nonce++);
                Assert.True(txResult.Success, $"Anchor block {b} failed: {txResult.RevertReason}");
                totalGas += (long)txResult.GasUsed;

                var verified = await VerifyAnchorOnChain(l1, hubAddress, 31337, (ulong)b,
                    block.StateRoot, block.TransactionsHash, block.ReceiptHash);
                Assert.True(verified, $"Anchor block {b} failed on-chain verification");
            }

            return (results, totalGas);
        }

        // ============================================================
        // TIER TESTS — all against real Hub contract
        // ============================================================

        [Fact]
        public async Task Hub_BatchRoot_NoProof_NoCarrier_NoDA()
        {
            var (l1, hub, nonce) = await DeployAndRegisterHub();
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(10, ws);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws);
            var (results, gas) = await AnchorToHub(l2, l1, hub, 5, pipeline, nonce);

            Assert.Equal(2, results.Count);
            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof));
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa));
                _output.WriteLine($"extraData={r.EncodedPayload.Length}b, sections={d.Sections.Count}");
            }

            _output.WriteLine($"Hub_BatchRoot_NoProof_NoCarrier_NoDA: 2 anchors, total gas={gas}, verified on-chain — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task Hub_BlockRoot_NoProof_NoCarrier_NoDA()
        {
            var (l1, hub, nonce) = await DeployAndRegisterHub();
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws);
            var (results, gas) = await AnchorToHub(l2, l1, hub, 1, pipeline, nonce);

            Assert.Equal(5, results.Count);
            _output.WriteLine($"Hub_BlockRoot_NoProof_NoCarrier_NoDA: 5 anchors, total gas={gas}, avg={gas / 5}/block — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task Hub_BlockRoot_ProvesPerBlock_CarrierInline_DACalldata()
        {
            var (l1, hub, nonce) = await DeployAndRegisterHub();
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5, ws, new MockBlockProver(), ProofCadence.Continuous);

            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws)
                .AddContributor(new InlineProofContributor(ws))
                .AddContributor(new CalldataDataContributor(ws));

            var (results, gas) = await AnchorToHub(l2, l1, hub, 1, pipeline, nonce);

            Assert.Equal(5, results.Count);
            bool anyProof = false;
            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                var proof = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof);
                var da = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa);
                Assert.NotNull(da);
                if (proof != null) anyProof = true;
                _output.WriteLine($"extraData={r.EncodedPayload.Length}b, proof={proof != null}, da={da.Bytes.Length}b");
            }
            Assert.True(anyProof);

            _output.WriteLine($"Hub_BlockRoot_ProvesPerBlock_CarrierInline_DACalldata: gas={gas}, avg={gas / 5}/block — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task Hub_BatchRoot_ProvesPerBatch_CarrierBlob_DABlob()
        {
            var (l1, hub, nonce) = await DeployAndRegisterHub();
            var ws = new InMemoryWitnessStore();
            var blobStore = new InMemoryBlobStore();
            var l2 = await ProduceL2(10, ws, new MockBlockProver(), ProofCadence.Continuous);

            var kzg = new MockBlobKzgProvider();
            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws)
                .WithProofPublisher(new BlobProofPublisher(kzg, blobStore))
                .WithDaPublisher(new BlobDataAvailabilityPublisher(kzg, blobStore));

            var (results, gas) = await AnchorToHub(l2, l1, hub, 5, pipeline, nonce);

            Assert.Equal(2, results.Count);
            foreach (var r in results)
            {
                var d = AnchorPayloadCodec.Decode(r.EncodedPayload);
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineProof));
                Assert.Null(AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.InlineDa));
                var daCommit = AnchorPayloadCodec.FindSection(d, AnchorPayloadSectionType.DaCommitment);
                Assert.NotNull(daCommit);
                _output.WriteLine($"extraData={r.EncodedPayload.Length}b (commitments only)");
            }

            _output.WriteLine($"Hub_BatchRoot_ProvesPerBatch_CarrierBlob_DABlob: gas={gas}, avg={gas / 2}/batch — PASS");
            l2.Dispose(); l1.Dispose();
        }

        [Fact]
        public async Task Hub_GasCostComparison()
        {
            // Run all configs and compare gas costs
            var configs = new (string name, int cadence, bool proof, bool blobMode)[]
            {
                ("BatchRoot_NoProof_NoCarrier_NoDA", 5, false, false),
                ("BlockRoot_NoProof_NoCarrier_NoDA", 1, false, false),
                ("BlockRoot_ProvesPerBlock_CarrierInline_DACalldata", 1, true, false),
                ("BatchRoot_ProvesPerBatch_CarrierBlob_DABlob", 5, true, true),
            };

            _output.WriteLine("=== GAS COST COMPARISON (10 L2 blocks) ===");
            _output.WriteLine($"{"Config",-52} {"Anchors",8} {"TotalGas",10} {"Avg/Anchor",10} {"ExtraData",10}");
            _output.WriteLine(new string('-', 95));

            foreach (var (name, cadence, proof, blobMode) in configs)
            {
                var (l1, hub, nonce) = await DeployAndRegisterHub();
                var ws = new InMemoryWitnessStore();
                var blobStore = new InMemoryBlobStore();
                var kzg = new MockBlobKzgProvider();
                var l2 = await ProduceL2(10, ws,
                    proof ? new MockBlockProver() : null,
                    proof ? ProofCadence.Continuous : ProofCadence.Off);

                var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, ws);
                if (proof && !blobMode)
                {
                    pipeline.AddContributor(new InlineProofContributor(ws));
                    pipeline.AddContributor(new CalldataDataContributor(ws));
                }
                if (blobMode)
                {
                    pipeline.WithProofPublisher(new BlobProofPublisher(kzg, blobStore));
                    pipeline.WithDaPublisher(new BlobDataAvailabilityPublisher(kzg, blobStore));
                }

                var (results, gas) = await AnchorToHub(l2, l1, hub, cadence, pipeline, nonce);
                var avgExtraData = 0;
                foreach (var r in results) avgExtraData += r.EncodedPayload.Length;
                avgExtraData /= results.Count;

                _output.WriteLine($"{name,-52} {results.Count,8} {gas,10} {gas / results.Count,10} {avgExtraData,10}b");

                l2.Dispose(); l1.Dispose();
            }

            _output.WriteLine("\nGas cost comparison: DONE");
        }
    }
}
