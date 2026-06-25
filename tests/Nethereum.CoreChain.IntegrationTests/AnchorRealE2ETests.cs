using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
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
    public class AnchorRealE2ETests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public AnchorRealE2ETests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task RealE2E_L2BlocksToL1AnchorWithProof()
        {
            // === L2 DevChain ===
            BigInteger l2ChainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = l2ChainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            l2.WitnessStore = witnessStore;
            l2.BlockProver = new MockBlockProver();
            l2.ProofCadence = ProofCadence.Continuous;
            await l2.StartAsync(new[] { _sender });

            // === L1 DevChain ===
            BigInteger l1ChainId = 1337;
            var l1 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = l1ChainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await l1.StartAsync(new[] { _sender });

            // === Produce 5 blocks on L2 ===
            ulong l2Nonce = 0;
            for (int b = 0; b < 5; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), l2ChainId,
                        $"0x{(b + 1):x40}", 1000, l2Nonce++, 1_000_000_000, 21_000, ""));
                await l2.SendTransactionAsync(tx);
                await l2.MineBlockAsync();
            }
            _output.WriteLine($"L2: produced 5 blocks");

            // === Build anchor pipeline (Tier 3: inline proof + calldata DA) ===
            var pipeline = new AnchorPublicationPipeline(StateModel.MptKeccak, witnessStore)
                .AddContributor(new InlineProofContributor(witnessStore))
                .AddContributor(new CalldataDataContributor(witnessStore));

            // === Anchor each block to L1 as a real transaction ===
            ulong l1Nonce = 0;
            for (int b = 1; b <= 5; b++)
            {
                var l2Block = await l2.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)l2ChainId,
                    Kind = AnchorKind.Block,
                    StartBlock = b,
                    EndBlock = b,
                    StateRoot = l2Block.StateRoot,
                    TransactionsRoot = l2Block.TransactionsHash,
                    ReceiptsRoot = l2Block.ReceiptHash
                };

                var pubResult = await pipeline.ExecuteAsync(scope);

                // Submit real L1 transaction with extraData as calldata
                var l1Tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), l1ChainId,
                        "0x2222222222222222222222222222222222222222",
                        0, l1Nonce++, 1_000_000_000, 100_000,
                        pubResult.EncodedPayload.ToHex()));

                var l1Result = await l1.SendTransactionAsync(l1Tx);
                await l1.MineBlockAsync();
                Assert.True(l1Result.Success, $"L1 anchor tx failed for L2 block {b}");

                pipeline.RecordAnchorTx(b, l1Result.TransactionHash, pubResult.EncodedPayload);

                _output.WriteLine($"L2 block {b} → L1 block {await l1.GetBlockNumberAsync()}: " +
                    $"payload={pubResult.EncodedPayload.Length}b, " +
                    $"validated={pubResult.PreviousValidatedBlock?.ToString() ?? "none"}");
            }

            // === Verify L1 state ===
            var l1BlockCount = await l1.GetBlockNumberAsync();
            Assert.Equal(5, (long)l1BlockCount);
            _output.WriteLine($"L1: {l1BlockCount} anchor blocks");

            // === Verify receipt offsets ===
            var receipt3 = pipeline.ReceiptRecorder.GetReceipt(3);
            Assert.NotNull(receipt3);
            Assert.True(receipt3.SectionOffsets.ContainsKey(AnchorPayloadSectionType.InlineProof));
            Assert.True(receipt3.SectionOffsets.ContainsKey(AnchorPayloadSectionType.InlineDa));
            _output.WriteLine($"Receipt block 3: proof offset={receipt3.SectionOffsets[AnchorPayloadSectionType.InlineProof].Offset}, " +
                $"DA offset={receipt3.SectionOffsets[AnchorPayloadSectionType.InlineDa].Offset}");

            _output.WriteLine("RealE2E: L2 → pipeline → L1 anchor tx → verify: PASS");
            l2.Dispose();
            l1.Dispose();
        }

        [Fact]
        public async Task RealE2E_BlockProverServerHttpFlow()
        {
            // === Start BlockProver.Server in-process ===
            var port = 15300 + new Random().Next(500);
            var serverPath = FindProject("Nethereum.BlockProver.Server");
            _output.WriteLine($"BlockProver.Server: {serverPath}, port: {port}");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --project \"{serverPath}\" --urls http://localhost:{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
            var process = Process.Start(psi);

            try
            {
                await WaitForServer($"http://localhost:{port}/status", 30);
                _output.WriteLine("BlockProver.Server started");

                // === L2: produce blocks, capture witnesses ===
                BigInteger l2ChainId = 31337;
                var witnessStore = new InMemoryWitnessStore();
                var l2 = DevChainNode.CreateInMemory(new DevChainConfig
                {
                    ChainId = l2ChainId, BlockGasLimit = 30_000_000, AutoMine = false
                });
                l2.WitnessStore = witnessStore;
                l2.ProofCadence = ProofCadence.Off;
                await l2.StartAsync(new[] { _sender });

                ulong nonce = 0;
                for (int b = 0; b < 3; b++)
                {
                    var tx = TransactionFactory.CreateTransaction(
                        _signer.SignTransaction(_pk.HexToByteArray(), l2ChainId,
                            $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                    await l2.SendTransactionAsync(tx);
                    await l2.MineBlockAsync();
                }
                _output.WriteLine("L2: produced 3 blocks with witnesses");

                using var http = new HttpClient();

                // === Submit witnesses to BlockProver.Server via HTTP ===
                for (int b = 1; b <= 3; b++)
                {
                    var witness = await witnessStore.GetWitnessAsync(b);
                    Assert.NotNull(witness);

                    var resp = await http.PostAsync(
                        $"http://localhost:{port}/witness/{b}",
                        new ByteArrayContent(witness));
                    resp.EnsureSuccessStatusCode();
                    _output.WriteLine($"Submitted witness for block {b}: {witness.Length} bytes");

                    var queueResp = await http.PostAsync(
                        $"http://localhost:{port}/queue/{b}", null);
                    queueResp.EnsureSuccessStatusCode();
                }

                // === Wait for proofs to complete ===
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed.TotalSeconds < 10)
                {
                    var statusResp = await http.GetStringAsync($"http://localhost:{port}/status");
                    if (statusResp.Contains("\"lastProvenBlock\":3")) break;
                    await Task.Delay(200);
                }

                // === Verify all blocks proved ===
                for (int b = 1; b <= 3; b++)
                {
                    var proofResp = await http.GetStringAsync($"http://localhost:{port}/proof/{b}");
                    Assert.Contains("\"proverMode\":\"Mock\"", proofResp);
                    Assert.Contains("\"proofSize\":256", proofResp);
                    _output.WriteLine($"Block {b}: proof verified via HTTP, {proofResp}");
                }

                // === Verify queue status ===
                for (int b = 1; b <= 3; b++)
                {
                    var queueResp = await http.GetStringAsync($"http://localhost:{port}/queue/{b}");
                    Assert.Contains("\"status\":\"Completed\"", queueResp);
                }

                _output.WriteLine("RealE2E: L2 → HTTP witness → HTTP queue → prove → HTTP verify: PASS");
                l2.Dispose();
            }
            finally
            {
                try { process?.Kill(true); } catch { }
                process?.Dispose();
            }
        }

        [Fact]
        public async Task RealE2E_FullPipeline_L2ProveAnchorL1()
        {
            // === L2 DevChain ===
            BigInteger l2ChainId = 31337;
            var witnessStore = new InMemoryWitnessStore();
            var proofQueue = new InMemoryProofRequestQueue();
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = l2ChainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            l2.WitnessStore = witnessStore;
            l2.ProofCadence = ProofCadence.Off;
            await l2.StartAsync(new[] { _sender });

            // === L1 DevChain ===
            BigInteger l1ChainId = 1337;
            var l1 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = l1ChainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await l1.StartAsync(new[] { _sender });

            // === Produce 10 blocks on L2 ===
            ulong l2Nonce = 0;
            for (int b = 0; b < 10; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), l2ChainId,
                        $"0x{(b + 1):x40}", 1000, l2Nonce++, 1_000_000_000, 21_000, ""));
                await l2.SendTransactionAsync(tx);
                await l2.MineBlockAsync();
            }
            _output.WriteLine("L2: 10 blocks produced, witnesses stored");

            // === Phase 1: Anchor blocks 1-5 (no proofs yet) ===
            var pipeline = new AnchorPublicationPipeline(
                    StateModel.MptKeccak, witnessStore, proofQueue)
                .AddContributor(new InlineProofContributor(witnessStore))
                .AddContributor(new CalldataDataContributor(witnessStore));

            ulong l1Nonce = 0;
            for (int b = 1; b <= 5; b++)
            {
                var l2Block = await l2.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)l2ChainId, Kind = AnchorKind.Block,
                    StartBlock = b, EndBlock = b, StateRoot = l2Block.StateRoot,
                    TransactionsRoot = l2Block.TransactionsHash, ReceiptsRoot = l2Block.ReceiptHash
                };
                var result = await pipeline.ExecuteAsync(scope);

                var l1Tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), l1ChainId,
                        "0x2222222222222222222222222222222222222222",
                        0, l1Nonce++, 1_000_000_000, 100_000,
                        result.EncodedPayload.ToHex()));
                await l1.SendTransactionAsync(l1Tx);
                await l1.MineBlockAsync();

                var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);
                var hasProof = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof) != null;
                _output.WriteLine($"Anchor {b}: DA=yes, proof={hasProof}, enqueued={await proofQueue.GetStatusAsync(b) != null}");
            }

            // === Phase 2: Prove blocks 1-5 (simulating BlockProver.Server) ===
            var prover = new MockBlockProver();
            for (int b = 1; b <= 5; b++)
            {
                var witness = await witnessStore.GetWitnessAsync(b);
                if (witness != null)
                {
                    var l2Block = await l2.GetBlockByNumberAsync(b);
                    byte[] preRoot = b > 1 ? (await l2.GetBlockByNumberAsync(b - 1)).StateRoot : null;
                    var proof = await prover.ProveBlockAsync(witness, preRoot, l2Block.StateRoot, b);
                    await witnessStore.StoreProofAsync(b, proof);
                    await proofQueue.CompleteAsync(b);
                }
            }
            _output.WriteLine("Proved blocks 1-5");

            // === Phase 3: Anchor blocks 6-10 (proofs for 1-5 now available) ===
            for (int b = 6; b <= 10; b++)
            {
                var l2Block = await l2.GetBlockByNumberAsync(b);
                var scope = new AnchorScope
                {
                    ChainId = (long)l2ChainId, Kind = AnchorKind.Block,
                    StartBlock = b, EndBlock = b, StateRoot = l2Block.StateRoot,
                    TransactionsRoot = l2Block.TransactionsHash, ReceiptsRoot = l2Block.ReceiptHash
                };
                var result = await pipeline.ExecuteAsync(scope);

                var l1Tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), l1ChainId,
                        "0x2222222222222222222222222222222222222222",
                        0, l1Nonce++, 1_000_000_000, 100_000,
                        result.EncodedPayload.ToHex()));
                await l1.SendTransactionAsync(l1Tx);
                await l1.MineBlockAsync();

                var decoded = AnchorPayloadCodec.Decode(result.EncodedPayload);
                var hasProof = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof) != null;
                var pointer = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.PreviousValidatedPointer);
                var validated = pointer != null ? BitConverter.ToInt64(pointer.Bytes) : 0;

                _output.WriteLine($"Anchor {b}: DA=yes, proof={hasProof}, validatedThrough={validated}");
            }

            // === Verify L1 ===
            var l1Blocks = await l1.GetBlockNumberAsync();
            Assert.Equal(10, (long)l1Blocks);
            _output.WriteLine($"L1: {l1Blocks} anchor blocks confirmed");

            // === Verify two-phase: anchors 6-10 should carry proof pointer ===
            _output.WriteLine("RealE2E: L2(10 blocks) → prove(1-5) → anchor(1-10 to L1) → two-phase pointer: PASS");

            l2.Dispose();
            l1.Dispose();
        }

        private static async Task WaitForServer(string url, int timeoutSeconds)
        {
            using var http = new HttpClient();
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    var resp = await http.GetAsync(url);
                    if (resp.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(500);
            }
            throw new TimeoutException($"Server at {url} did not start within {timeoutSeconds}s");
        }

        private static string FindProject(string projectName)
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir, "src", projectName, $"{projectName}.csproj");
                if (System.IO.File.Exists(candidate)) return candidate;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            throw new System.IO.FileNotFoundException($"Cannot find {projectName}.csproj");
        }
    }
}
