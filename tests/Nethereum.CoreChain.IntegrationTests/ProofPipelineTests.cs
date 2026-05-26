using System.Numerics;
using System.Threading.Tasks;
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
    public class ProofPipelineTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public ProofPipelineTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task ShouldCaptureWitnessAndMockProve()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();

            await node.StartAsync(new[] { _sender });

            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    "0x1111111111111111111111111111111111111111",
                    1000, 0, 1_000_000_000, 21_000, ""));
            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            _output.WriteLine($"Block: {blockNumber}");

            var witness = await node.WitnessStore.GetWitnessAsync(blockNumber);
            Assert.NotNull(witness);
            _output.WriteLine($"Witness bytes: {witness.Length}");

            var proof = await node.WitnessStore.GetProofAsync(blockNumber);
            Assert.NotNull(proof);
            Assert.NotNull(proof.ProofBytes);
            Assert.Equal(MockBlockProver.Groth16ProofSize, proof.ProofBytes.Length);
            Assert.Equal("Mock", proof.ProverMode);
            Assert.Equal((long)blockNumber, proof.BlockNumber);
            Assert.NotNull(proof.PreStateRoot);
            Assert.NotNull(proof.PostStateRoot);

            _output.WriteLine($"Proof: {proof.ProofBytes.ToHex(true)}");
            _output.WriteLine($"Pre-state root: {proof.PreStateRoot.ToHex(true)}");
            _output.WriteLine($"Post-state root: {proof.PostStateRoot.ToHex(true)}");
            _output.WriteLine($"Mode: {proof.ProverMode}");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldProveMultipleBlocks()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();

            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 5; b++)
            {
                for (int t = 0; t < 3; t++)
                {
                    var to = $"0x{(b * 3 + t + 1):x40}";
                    var tx = TransactionFactory.CreateTransaction(
                        _signer.SignTransaction(_pk.HexToByteArray(), chainId, to,
                            1000, nonce++, 1_000_000_000, 21_000, ""));
                    await node.SendTransactionAsync(tx);
                }
                await node.MineBlockAsync();
            }

            for (int b = 1; b <= 5; b++)
            {
                var proof = await node.WitnessStore.GetProofAsync(b);
                Assert.NotNull(proof);
                Assert.Equal(b, proof.BlockNumber);
                Assert.NotNull(proof.PreStateRoot);
                Assert.NotNull(proof.PostStateRoot);
                Assert.NotEqual(proof.PreStateRoot, proof.PostStateRoot);
            }

            _output.WriteLine("5 blocks proved successfully");
            node.Dispose();
        }

        [Fact]
        public async Task ShouldReplayBlockForWitnessWithoutMutatingState()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await node.StartAsync(new[] { _sender });

            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    "0x1111111111111111111111111111111111111111",
                    1000, 0, 1_000_000_000, 21_000, ""));
            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            var stateRootBefore = (await node.GetBlockByNumberAsync(blockNumber)).StateRoot;
            var senderBalanceBefore = await node.GetBalanceAsync(_sender);

            var witness = await node.CaptureBlockWitnessAsync((long)blockNumber);

            var stateRootAfter = (await node.GetBlockByNumberAsync(blockNumber)).StateRoot;
            var senderBalanceAfter = await node.GetBalanceAsync(_sender);
            var latestBlockAfter = await node.GetBlockNumberAsync();

            Assert.Equal(stateRootBefore, stateRootAfter);
            Assert.Equal(senderBalanceBefore, senderBalanceAfter);
            Assert.Equal(blockNumber, latestBlockAfter);

            Assert.NotNull(witness);
            Assert.True(witness.Length > 100, $"Witness should be substantial, got {witness.Length} bytes");

            var deserialized = BinaryBlockWitness.Deserialize(witness);
            Assert.Equal((long)blockNumber, deserialized.BlockNumber);
            Assert.True(deserialized.Accounts.Count > 0, "Should have recorded accounts");
            Assert.Single(deserialized.Transactions);
            Assert.Equal(_sender.ToLower(), deserialized.Transactions[0].From.ToLower());

            _output.WriteLine($"Block {blockNumber}: replay witness = {witness.Length} bytes");
            _output.WriteLine($"Accounts recorded: {deserialized.Accounts.Count}");
            _output.WriteLine($"State root unchanged: {stateRootBefore.ToHex(true)}");
            _output.WriteLine("Replay is read-only: VERIFIED");

            node.Dispose();
        }

        [Fact]
        public async Task ShouldNotProveWithoutProverConfigured()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });

            await node.StartAsync(new[] { _sender });

            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    "0x1111111111111111111111111111111111111111",
                    1000, 0, 1_000_000_000, 21_000, ""));
            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            Assert.Null(node.WitnessStore);
            node.Dispose();
        }

        [Fact]
        public async Task ShouldRespectPeriodicCadence()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = CoreChain.Proving.ProofCadence.Periodic(3);
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 6; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            for (int b = 1; b <= 6; b++)
            {
                var witness = await node.WitnessStore.GetWitnessAsync(b);
                Assert.NotNull(witness);

                var proof = await node.WitnessStore.GetProofAsync(b);
                if (b % 3 == 0)
                {
                    Assert.NotNull(proof);
                    _output.WriteLine($"Block {b}: proved (periodic hit)");
                }
                else
                {
                    Assert.Null(proof);
                    _output.WriteLine($"Block {b}: witness only (periodic skip)");
                }
            }

            node.Dispose();
        }

        [Fact]
        public async Task ShouldProveOnDemand()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = CoreChain.Proving.ProofCadence.OnDemand;
            await node.StartAsync(new[] { _sender });

            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    "0x1111111111111111111111111111111111111111",
                    1000, 0, 1_000_000_000, 21_000, ""));
            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            var proof = await node.WitnessStore.GetProofAsync(1);
            Assert.Null(proof);
            _output.WriteLine("Block 1: no auto proof (OnDemand mode)");

            var witness = await node.WitnessStore.GetWitnessAsync(1);
            Assert.NotNull(witness);
            _output.WriteLine($"Block 1: witness stored ({witness.Length} bytes)");

            var result = await node.ProveBlockOnDemandAsync(1);
            Assert.NotNull(result);
            Assert.NotNull(result.ProofBytes);
            Assert.Equal("Mock", result.ProverMode);
            _output.WriteLine($"Block 1: proved on demand, proof={result.ProofBytes.ToHex(true)}");

            var storedProof = await node.WitnessStore.GetProofAsync(1);
            Assert.NotNull(storedProof);

            node.Dispose();
        }

        [Fact]
        public async Task ShouldPurgeWitnessesUntilProven()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = CoreChain.Proving.ProofCadence.Continuous;
            node.WitnessRetention = CoreChain.Proving.WitnessRetentionPolicy.UntilProven;
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

            for (int b = 1; b <= 3; b++)
            {
                var proof = await node.WitnessStore.GetProofAsync(b);
                Assert.NotNull(proof);

                var witness = await node.WitnessStore.GetWitnessAsync(b);
                Assert.Null(witness);
                _output.WriteLine($"Block {b}: proof exists, witness purged (UntilProven)");
            }

            node.Dispose();
        }

        [Fact]
        public async Task ShouldPurgeWitnessesByBlockCount()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.ProofCadence = CoreChain.Proving.ProofCadence.Off;
            node.WitnessRetention = CoreChain.Proving.WitnessRetentionPolicy.Blocks(3);
            await node.StartAsync(new[] { _sender });

            ulong nonce = 0;
            for (int b = 0; b < 6; b++)
            {
                var tx = TransactionFactory.CreateTransaction(
                    _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                        $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, ""));
                await node.SendTransactionAsync(tx);
                await node.MineBlockAsync();
            }

            for (int b = 1; b <= 3; b++)
            {
                var witness = await node.WitnessStore.GetWitnessAsync(b);
                Assert.Null(witness);
                _output.WriteLine($"Block {b}: witness purged (older than 3 blocks)");
            }

            for (int b = 4; b <= 6; b++)
            {
                var witness = await node.WitnessStore.GetWitnessAsync(b);
                Assert.NotNull(witness);
                _output.WriteLine($"Block {b}: witness retained ({witness.Length} bytes)");
            }

            node.Dispose();
        }

        [Fact]
        public async Task ShouldRetainWitnessesForever()
        {
            BigInteger chainId = 31337;
            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            node.WitnessStore = new InMemoryWitnessStore();
            node.BlockProver = new MockBlockProver();
            node.ProofCadence = CoreChain.Proving.ProofCadence.Continuous;
            node.WitnessRetention = CoreChain.Proving.WitnessRetentionPolicy.Forever;
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
                var witness = await node.WitnessStore.GetWitnessAsync(b);
                Assert.NotNull(witness);
                var proof = await node.WitnessStore.GetProofAsync(b);
                Assert.NotNull(proof);
            }

            _output.WriteLine("All 5 blocks: witnesses + proofs retained (Forever)");
            node.Dispose();
        }
    }
}
