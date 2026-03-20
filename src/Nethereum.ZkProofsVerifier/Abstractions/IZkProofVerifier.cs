using Org.BouncyCastle.Math;

namespace Nethereum.ZkProofsVerifier.Abstractions
{
    internal interface IZkProofVerifier<TProof, TVerificationKey>
    {
        ZkVerificationResult Verify(TProof proof, TVerificationKey vk, BigInteger[] publicInputs);
    }
}
