namespace Nethereum.EVM.Precompiles.Kzg
{
    public interface IKzgOperations
    {
        bool VerifyKzgProof(byte[] commitment, byte[] z, byte[] y, byte[] proof);
        byte[] ComputeVersionedHash(byte[] commitment);
        bool IsInitialized { get; }
    }
}
