using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BlobTransactionRoundTripTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _pk = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        public BlobTransactionRoundTripTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        [NethereumDocExample(DocSection.InProcessNode, "eip4844-blob-roundtrip", "Send blob tx to DevChain and decode stored blobs")]
        public async Task ShouldRoundTripBlobDataViaDevChain()
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            var builder = new BlobSidecarBuilder(kzg);

            var originalData = Encoding.UTF8.GetBytes("Hello Ethereum Blobs from Nethereum DevChain!");

            var (sidecar, versionedHashes) = builder.BuildFromData(originalData);
            Assert.Single(versionedHashes);
            Assert.Equal(0x01, versionedHashes[0][0]);

            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = 31337,
                BlockGasLimit = 30_000_000,
                AutoMine = false
            });
            await node.StartAsync(new[] { _sender });

            var tx = new Transaction4844(
                chainId: (Util.EvmUInt256)31337,
                nonce: (Util.EvmUInt256)0,
                maxPriorityFeePerGas: (Util.EvmUInt256)2000000000,
                maxFeePerGas: (Util.EvmUInt256)10000000000,
                gasLimit: (Util.EvmUInt256)21000,
                receiverAddress: "0x1111111111111111111111111111111111111111",
                amount: (Util.EvmUInt256)0,
                data: null,
                accessList: null,
                maxFeePerBlobGas: (Util.EvmUInt256)10000000000,
                blobVersionedHashes: versionedHashes);

            tx.Sidecar = sidecar;

            var signer = new Transaction4844Signer();
            signer.SignTransaction(_pk.HexToByteArray(), tx);

            var result = await node.SendTransactionAsync(tx);
            _output.WriteLine($"Tx success: {result.Success}, hash: {result.TransactionHash?.ToHex(true)}");

            await node.MineBlockAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            var storedBlobs = await node.BlobStore.GetBlobsByBlockNumberAsync(blockNumber);

            Assert.NotEmpty(storedBlobs);
            Assert.Single(storedBlobs);
            Assert.Equal(BlobEncoder.BLOB_SIZE, storedBlobs[0].Blob.Length);
            Assert.Equal(48, storedBlobs[0].KzgCommitment.Length);
            Assert.Equal(48, storedBlobs[0].KzgProof.Length);
            Assert.Equal(versionedHashes[0], storedBlobs[0].VersionedHash);

            var decodedBlobs = new System.Collections.Generic.List<byte[]>();
            foreach (var record in storedBlobs)
                decodedBlobs.Add(record.Blob);

            var decodedData = BlobEncoder.DecodeBlobs(decodedBlobs);
            var decodedText = Encoding.UTF8.GetString(decodedData);

            _output.WriteLine($"Decoded: {decodedText}");
            Assert.Equal("Hello Ethereum Blobs from Nethereum DevChain!", decodedText);

            node.Dispose();
        }

        [Fact]
        public async Task ShouldStoreMultipleBlobs()
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            var builder = new BlobSidecarBuilder(kzg);

            var largeData = new byte[BlobEncoder.USABLE_BYTES_PER_BLOB + 100];
            for (int i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 251 + 1);

            var (sidecar, versionedHashes) = builder.BuildFromData(largeData);
            Assert.Equal(2, versionedHashes.Count);
            Assert.Equal(2, sidecar.Blobs.Count);

            var node = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await node.StartAsync(new[] { _sender });

            var tx = new Transaction4844(
                chainId: (Util.EvmUInt256)31337,
                nonce: (Util.EvmUInt256)0,
                maxPriorityFeePerGas: (Util.EvmUInt256)2000000000,
                maxFeePerGas: (Util.EvmUInt256)10000000000,
                gasLimit: (Util.EvmUInt256)21000,
                receiverAddress: "0x1111111111111111111111111111111111111111",
                amount: (Util.EvmUInt256)0,
                data: null,
                accessList: null,
                maxFeePerBlobGas: (Util.EvmUInt256)10000000000,
                blobVersionedHashes: versionedHashes);
            tx.Sidecar = sidecar;

            var signerObj = new Transaction4844Signer();
            signerObj.SignTransaction(_pk.HexToByteArray(), tx);

            await node.SendTransactionAsync(tx);
            await node.MineBlockAsync();

            var blockNumber = await node.GetBlockNumberAsync();
            var storedBlobs = await node.BlobStore.GetBlobsByBlockNumberAsync(blockNumber);
            Assert.Equal(2, storedBlobs.Count);

            var blobs = new System.Collections.Generic.List<byte[]> { storedBlobs[0].Blob, storedBlobs[1].Blob };
            var decoded = BlobEncoder.DecodeBlobs(blobs);
            Assert.Equal(largeData, decoded);

            node.Dispose();
        }
    }
}
