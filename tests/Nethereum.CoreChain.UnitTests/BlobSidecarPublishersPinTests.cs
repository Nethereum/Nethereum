using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlobSidecarPublishersPinTests
    {
        private static readonly AnchorScope Scope = new AnchorScope { ChainId = 1, StartBlock = 4, EndBlock = 9 };

        private static byte[] Payload(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 7 + 1)).ToArray();

        private static byte[] Sha256OfVersionedHashes(System.Collections.Generic.List<BlobSidecarRecord> records)
        {
            var all = records.SelectMany(r => r.VersionedHash).ToArray();
            using var sha = SHA256.Create();
            return sha.ComputeHash(all);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(300_000)]
        public async Task Given_Payload_When_PublishedAsDaAndAsProof_Then_CommitmentHashEqualsSha256OfVersionedHashes(int length)
        {
            var store = new InMemoryBlobStore();
            var kzg = new MockBlobKzgProvider();
            var data = Payload(length);

            var da = await new BlobDataAvailabilityPublisher(kzg, store)
                .PublishAsync(new DaPayload { Data = data }, Scope);
            var proof = await new BlobProofPublisher(kzg, store)
                .PublishAsync(new ProofBundle { ProofBytes = Payload(length), ElfHash = new byte[32], ProverMode = "Mock", BlockNumber = 9 }, Scope);

            var daRecords = await store.GetBlobsByTransactionHashAsync(da.Commitment.CommitmentHash);
            var proofRecords = await store.GetBlobsByTransactionHashAsync(proof.CommitmentHash);
            Assert.Equal(DaMode.Public, da.Commitment.Type);
            Assert.Equal(length, da.Commitment.Length);
            Assert.Equal(da.Commitment.CommitmentHash, da.Commitment.TransactionHash);
            Assert.Equal(Sha256OfVersionedHashes(daRecords), da.Commitment.CommitmentHash);
            Assert.Equal(proof.CommitmentHash, proof.TransactionHash);
            Assert.Equal(Sha256OfVersionedHashes(proofRecords), proof.CommitmentHash);
            Assert.Equal(Enumerable.Range(0, daRecords.Count), daRecords.Select(r => r.Index));
            Assert.All(daRecords, r =>
            {
                Assert.Equal(kzg.BlobToKzgCommitment(r.Blob), r.KzgCommitment);
                Assert.Equal(kzg.ComputeBlobKzgProof(r.Blob, r.KzgCommitment), r.KzgProof);
                Assert.Equal(kzg.ComputeVersionedHash(r.KzgCommitment), r.VersionedHash);
            });
            Assert.NotEmpty(await store.GetBlobsByBlockNumberAsync(9));
        }

        [Fact]
        public async Task Given_StoredBlobs_When_Retrieved_Then_RoundTripsPayload()
        {
            var store = new InMemoryBlobStore();
            var kzg = new MockBlobKzgProvider();
            var data = Payload(300_000);
            var proofBytes = Payload(1234);
            var daPublisher = new BlobDataAvailabilityPublisher(kzg, store);
            var proofPublisher = new BlobProofPublisher(kzg, store);

            var da = await daPublisher.PublishAsync(new DaPayload { Data = data }, Scope);
            var proof = await proofPublisher.PublishAsync(
                new ProofBundle { ProofBytes = proofBytes, ElfHash = new byte[32], ProverMode = "Mock", BlockNumber = 9 }, Scope);

            var retrievedData = await daPublisher.RetrieveAsync(da.Commitment);
            var retrievedProof = await proofPublisher.RetrieveAsync(proof);
            Assert.StartsWith(System.Convert.ToHexString(data), System.Convert.ToHexString(retrievedData));
            Assert.Equal(proofBytes, retrievedProof);
        }

        [Fact]
        public async Task Given_UnknownCommitment_When_Retrieved_Then_ReturnsNull()
        {
            var store = new InMemoryBlobStore();
            var kzg = new MockBlobKzgProvider();
            var daPublisher = new BlobDataAvailabilityPublisher(kzg, store);
            var proofPublisher = new BlobProofPublisher(kzg, store);

            Assert.Null(await daPublisher.RetrieveAsync(null));
            Assert.Null(await daPublisher.RetrieveAsync(new DaCommitment()));
            Assert.Null(await daPublisher.RetrieveAsync(new DaCommitment { CommitmentHash = new byte[32] }));
            Assert.Null(await proofPublisher.RetrieveAsync(null));
            Assert.Null(await proofPublisher.RetrieveAsync(new ProofPublication()));
            Assert.Null(await proofPublisher.RetrieveAsync(new ProofPublication { CommitmentHash = new byte[32] }));
        }

        [Fact]
        public async Task Given_EmptyPayloadOrProof_When_Published_Then_NothingIsStored()
        {
            var store = new InMemoryBlobStore();
            var kzg = new MockBlobKzgProvider();

            var da = await new BlobDataAvailabilityPublisher(kzg, store)
                .PublishAsync(new DaPayload { Data = new byte[0] }, Scope);
            var daNull = await new BlobDataAvailabilityPublisher(kzg, store).PublishAsync(null, Scope);
            var proof = await new BlobProofPublisher(kzg, store)
                .PublishAsync(new ProofBundle { ProofBytes = new byte[0] }, Scope);
            var proofNull = await new BlobProofPublisher(kzg, store).PublishAsync(null, Scope);

            Assert.Null(da.Commitment);
            Assert.Null(daNull.Commitment);
            Assert.Null(proof);
            Assert.Null(proofNull);
            Assert.False(await store.HasBlobsForBlockAsync(9));
        }

        [Fact]
        public void Given_NullCollaborators_When_PublishersAreBuilt_Then_ArgumentNullExceptionNamesTheParameter()
        {
            var store = new InMemoryBlobStore();
            var kzg = new MockBlobKzgProvider();

            Assert.Equal("kzg", Assert.Throws<System.ArgumentNullException>(() => new BlobDataAvailabilityPublisher(null, store)).ParamName);
            Assert.Equal("blobStore", Assert.Throws<System.ArgumentNullException>(() => new BlobDataAvailabilityPublisher(kzg, null)).ParamName);
            Assert.Equal("kzg", Assert.Throws<System.ArgumentNullException>(() => new BlobProofPublisher(null, store)).ParamName);
            Assert.Equal("blobStore", Assert.Throws<System.ArgumentNullException>(() => new BlobProofPublisher(kzg, null)).ParamName);
        }
    }
}
