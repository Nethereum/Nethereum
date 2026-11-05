using System;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Abstractions;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.ZkProofsVerifier.Groth16
{
    public class Groth16Verifier : IZkProofVerifier<Groth16Proof, Groth16VerificationKey>
    {
        public ZkVerificationResult Verify(Groth16Proof proof, Groth16VerificationKey vk, BigInteger[] publicInputs)
        {
            return Check(proof, vk, publicInputs, true);
        }

        internal ZkVerificationResult VerifyParsed(Groth16Proof proof, Groth16VerificationKey vk, BigInteger[] publicInputs)
        {
            return Check(proof, vk, publicInputs, false);
        }

        private static ZkVerificationResult Check(Groth16Proof proof, Groth16VerificationKey vk, BigInteger[] publicInputs, bool validatePoints)
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

            for (int i = 0; i < publicInputs.Length; i++)
            {
                if (publicInputs[i] == null || publicInputs[i].SignValue < 0 || publicInputs[i].CompareTo(BN128Constants.Order) >= 0)
                    return ZkVerificationResult.Invalid(string.Format("Public input {0} is not a canonical scalar field element", i));
            }

            if (validatePoints)
            {
                var pointError = Circom.Groth16PointValidator.ValidateProof(proof);
                if (pointError != null)
                    return ZkVerificationResult.Invalid(pointError);
            }

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
