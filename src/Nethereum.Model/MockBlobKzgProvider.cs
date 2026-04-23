using System.Security.Cryptography;

namespace Nethereum.Model
{
    public class MockBlobKzgProvider : IBlobKzgProvider
    {
        public const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public byte[] BlobToKzgCommitment(byte[] blob)
        {
            if (blob == null || blob.Length != BlobEncoder.BLOB_SIZE)
                throw new System.ArgumentException($"Blob must be {BlobEncoder.BLOB_SIZE} bytes");

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(blob);
                var commitment = new byte[48];
                System.Array.Copy(hash, 0, commitment, 0, 32);
                return commitment;
            }
        }

        public byte[] ComputeBlobKzgProof(byte[] blob, byte[] commitment)
        {
            if (blob == null || blob.Length != BlobEncoder.BLOB_SIZE)
                throw new System.ArgumentException($"Blob must be {BlobEncoder.BLOB_SIZE} bytes");

            using (var sha = SHA256.Create())
            {
                var combined = new byte[blob.Length + commitment.Length];
                System.Array.Copy(blob, 0, combined, 0, blob.Length);
                System.Array.Copy(commitment, 0, combined, blob.Length, commitment.Length);
                var hash = sha.ComputeHash(combined);
                var proof = new byte[48];
                System.Array.Copy(hash, 0, proof, 0, 32);
                return proof;
            }
        }

        public byte[] ComputeVersionedHash(byte[] commitment)
        {
            if (commitment == null || commitment.Length != 48)
                throw new System.ArgumentException("Commitment must be 48 bytes");

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(commitment);
                hash[0] = VERSIONED_HASH_VERSION_KZG;
                return hash;
            }
        }
    }
}
