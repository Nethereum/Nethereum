using Org.BouncyCastle.Math;

namespace Nethereum.ZkProofsVerifier.Abstractions
{
    public interface IZkProofVerifier<TProof, TVerificationKey>
    {
        ZkVerificationResult Verify(TProof proof, TVerificationKey vk, BigInteger[] publicInputs);
    }
}
