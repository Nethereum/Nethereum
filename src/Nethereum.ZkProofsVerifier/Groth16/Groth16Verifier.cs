using System;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Abstractions;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.ZkProofsVerifier.Groth16
{
    internal class Groth16Verifier : IZkProofVerifier<Groth16Proof, Groth16VerificationKey>
    {
        public ZkVerificationResult Verify(Groth16Proof proof, Groth16VerificationKey vk, BigInteger[] publicInputs)
        {
            if (proof == null)
                return ZkVerificationResult.Invalid("Proof is null");
            if (vk == null)
                return ZkVerificationResult.Invalid("Verification key is null");
            if (publicInputs == null)
                return ZkVerificationResult.Invalid("Public inputs array is null");

            if (vk.IC == null || vk.IC.Length == 0)
                return ZkVerificationResult.Invalid("Verification key IC array is empty");

            int expectedInputs = vk.IC.Length - 1;
            if (publicInputs.Length != expectedInputs)
                return ZkVerificationResult.Invalid(
                    string.Format("Expected {0} public inputs but got {1}", expectedInputs, publicInputs.Length));

            try
            {
                ECPoint vkX = vk.IC[0];
                for (int i = 0; i < publicInputs.Length; i++)
                {
                    vkX = vkX.Add(vk.IC[i + 1].Multiply(publicInputs[i]));
                }

                ECPoint negA = proof.A.Negate();

                var g1Points = new ECPoint[] { negA, vk.Alpha, vkX, proof.C };
                var g2Points = new TwistPoint[] { proof.B, vk.Beta, vk.Gamma, vk.Delta };

                bool result = BN128Pairing.PairingCheck(g1Points, g2Points);
                return result ? ZkVerificationResult.Valid() : ZkVerificationResult.Invalid("Pairing check failed");
            }
            catch (Exception ex)
            {
                return ZkVerificationResult.Invalid("Verification error: " + ex.Message);
            }
        }
    }
}
