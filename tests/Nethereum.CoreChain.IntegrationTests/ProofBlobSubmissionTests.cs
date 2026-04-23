using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class ProofBlobSubmissionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly LegacyTransactionSigner _signer = new();

        public ProofBlobSubmissionTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task ShouldSubmitProofAsBlobToL1()
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            BigInteger chainId = 31337;

            // L2 DevChain — produces blocks with mock proofs
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = chainId, BlockGasLimit = 30_000_000, AutoMine = false
            });
            l2.WitnessStore = new InMemoryWitnessStore();
            l2.BlockProver = new MockBlockProver();
            await l2.StartAsync(new[] { _sender });

            // L1 DevChain — receives proof blobs
            var l1 = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = 1337, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await l1.StartAsync(new[] { _sender });

            // Produce a block on L2
            var tx = TransactionFactory.CreateTransaction(
                _signer.SignTransaction(_pk.HexToByteArray(), chainId,
                    "0x1111111111111111111111111111111111111111",
                    1000, 0, 1_000_000_000, 21_000, ""));
            await l2.SendTransactionAsync(tx);
            await l2.MineBlockAsync();

            var l2Block = await l2.GetBlockNumberAsync();
            var proof = await l2.WitnessStore.GetProofAsync(l2Block);
            Assert.NotNull(proof);
            _output.WriteLine($"L2 block {l2Block}: proof mode={proof.ProverMode}");

            // Serialize proof and submit as blob to L1
            var proofPayload = BlockProofSubmitter.SerializeProofPayload(proof);
            _output.WriteLine($"Proof payload: {proofPayload.Length} bytes");

            var (sidecar, versionedHashes) = new BlobSidecarBuilder(kzg).BuildFromData(proofPayload);

            var l1Tx = new Transaction4844(
                chainId: (Util.EvmUInt256)1337,
                nonce: (Util.EvmUInt256)0,
                maxPriorityFeePerGas: (Util.EvmUInt256)2000000000,
                maxFeePerGas: (Util.EvmUInt256)10000000000,
                gasLimit: (Util.EvmUInt256)21000,
                receiverAddress: "0x2222222222222222222222222222222222222222",
                amount: (Util.EvmUInt256)0,
                data: null,
                accessList: null,
                maxFeePerBlobGas: (Util.EvmUInt256)10000000000,
                blobVersionedHashes: versionedHashes);
            l1Tx.Sidecar = sidecar;

            var l1Signer = new Transaction4844Signer();
            l1Signer.SignTransaction(_pk.HexToByteArray(), l1Tx);

            var l1Result = await l1.SendTransactionAsync(l1Tx);
            await l1.MineBlockAsync();
            _output.WriteLine($"L1 tx success: {l1Result.Success}");

            // Fetch blob from L1 and decode proof
            var l1Block = await l1.GetBlockNumberAsync();
            var storedBlobs = await l1.BlobStore.GetBlobsByBlockNumberAsync(l1Block);
            Assert.NotEmpty(storedBlobs);

            var blobData = new System.Collections.Generic.List<byte[]>();
            foreach (var b in storedBlobs) blobData.Add(b.Blob);
            var decoded = BlobEncoder.DecodeBlobs(blobData);

            var recoveredProof = BlockProofSubmitter.DeserializeProofPayload(decoded);
            Assert.Equal(proof.BlockNumber, recoveredProof.BlockNumber);
            Assert.Equal(proof.ProverMode, recoveredProof.ProverMode);
            Assert.Equal(proof.PreStateRoot, recoveredProof.PreStateRoot);
            Assert.Equal(proof.PostStateRoot, recoveredProof.PostStateRoot);
            Assert.Equal(proof.ProofBytes, recoveredProof.ProofBytes);

            _output.WriteLine($"Round-trip: L2 block {recoveredProof.BlockNumber}, mode={recoveredProof.ProverMode}");
            _output.WriteLine("L2 → proof → blob → L1 → fetch → decode → verify: SUCCESS");

            l2.Dispose();
            l1.Dispose();
        }
    }
}
