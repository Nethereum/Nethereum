using System.Collections.Generic;
using System.Text;
using Nethereum.Documentation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class MockBlobKzgProviderTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "eip4844-blob-encoding", "Pluggable KZG — mock provider for testing")]
        public void ShouldProduceValidStructure()
        {
            var kzg = new MockBlobKzgProvider();
            var blob = new byte[BlobEncoder.BLOB_SIZE];
            blob[0] = 0x42;

            var commitment = kzg.BlobToKzgCommitment(blob);
            Assert.Equal(48, commitment.Length);

            var proof = kzg.ComputeBlobKzgProof(blob, commitment);
            Assert.Equal(48, proof.Length);

            var versionedHash = kzg.ComputeVersionedHash(commitment);
            Assert.Equal(32, versionedHash.Length);
            Assert.Equal(0x01, versionedHash[0]);
        }

        [Fact]
        public void ShouldBeDeterministic()
        {
            var kzg = new MockBlobKzgProvider();
            var blob = new byte[BlobEncoder.BLOB_SIZE];
            blob[100] = 0xFF;

            var c1 = kzg.BlobToKzgCommitment(blob);
            var c2 = kzg.BlobToKzgCommitment(blob);
            Assert.Equal(c1, c2);

            var p1 = kzg.ComputeBlobKzgProof(blob, c1);
            var p2 = kzg.ComputeBlobKzgProof(blob, c1);
            Assert.Equal(p1, p2);
        }

        [Fact]
        public void ShouldWorkWithBlobEncoderPipeline()
        {
            var kzg = new MockBlobKzgProvider();
            var data = Encoding.UTF8.GetBytes("Test data for mock KZG");
            var blobs = BlobEncoder.EncodeBlobs(data);

            var commitments = new List<byte[]>();
            var proofs = new List<byte[]>();
            var versionedHashes = new List<byte[]>();

            foreach (var blob in blobs)
            {
                var commitment = kzg.BlobToKzgCommitment(blob);
                var proof = kzg.ComputeBlobKzgProof(blob, commitment);
                var hash = kzg.ComputeVersionedHash(commitment);
                commitments.Add(commitment);
                proofs.Add(proof);
                versionedHashes.Add(hash);
            }

            var sidecar = new BlobSidecar(blobs, commitments, proofs);
            Assert.Single(sidecar.Blobs);
            Assert.Single(sidecar.Commitments);
            Assert.Single(sidecar.Proofs);
            Assert.Single(versionedHashes);

            var tx = new Transaction4844(
                chainId: 1, nonce: 0, maxPriorityFeePerGas: 1, maxFeePerGas: 1,
                gasLimit: 21000, receiverAddress: "0x1111111111111111111111111111111111111111",
                amount: 0, data: null, accessList: null,
                maxFeePerBlobGas: 1, blobVersionedHashes: versionedHashes);
            tx.Sidecar = sidecar;

            var encoded = tx.GetRLPEncodedWithSidecar();
            Assert.NotNull(encoded);
            Assert.True(encoded.Length > BlobEncoder.BLOB_SIZE);
        }

        [Fact]
        public void ShouldDifferFromRealKzgOutput()
        {
            var kzg = new MockBlobKzgProvider();
            var blob = new byte[BlobEncoder.BLOB_SIZE];

            var commitment = kzg.BlobToKzgCommitment(blob);
            Assert.NotEqual(new byte[48], commitment);
        }
    }
}
