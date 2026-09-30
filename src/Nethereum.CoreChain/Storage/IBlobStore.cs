using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public class BlobSidecarRecord
    {
        public int Index { get; set; }
        public byte[] Blob { get; set; }
        public byte[] KzgCommitment { get; set; }
        public byte[] KzgProof { get; set; }
        public byte[] VersionedHash { get; set; }
        public byte[] TransactionHash { get; set; }
    }

    public interface IBlobStore
    {
        Task StoreBlobsAsync(BigInteger blockNumber, byte[] transactionHash, List<BlobSidecarRecord> blobs);
        Task<List<BlobSidecarRecord>> GetBlobsByBlockNumberAsync(BigInteger blockNumber);
        Task<List<BlobSidecarRecord>> GetBlobsByTransactionHashAsync(byte[] transactionHash);
        Task<BlobSidecarRecord> GetBlobByVersionedHashAsync(byte[] versionedHash);
        Task<bool> HasBlobsForBlockAsync(BigInteger blockNumber);
    }
}
