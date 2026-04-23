namespace Nethereum.EVM.Precompiles.Kzg
{
    public interface IKzgOperations
    {
        bool VerifyKzgProof(byte[] commitment, byte[] z, byte[] y, byte[] proof);
        byte[] ComputeVersionedHash(byte[] commitment);
        byte[] BlobToKzgCommitment(byte[] blob);
        byte[] ComputeBlobKzgProof(byte[] blob, byte[] commitment);
        bool VerifyBlobKzgProof(byte[] blob, byte[] commitment, byte[] proof);
        bool IsInitialized { get; }
    }
}
