namespace Nethereum.Model
{
    public interface IBlobKzgProvider
    {
        byte[] BlobToKzgCommitment(byte[] blob);
        byte[] ComputeBlobKzgProof(byte[] blob, byte[] commitment);
        byte[] ComputeVersionedHash(byte[] commitment);
    }
}
