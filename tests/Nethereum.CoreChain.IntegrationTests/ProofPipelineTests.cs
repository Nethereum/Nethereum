using System.Numerics;
using System.Threading.Tasks;
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
            Assert.Equal(32, proof.ProofBytes.Length);
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
    }
}
