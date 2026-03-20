using Nethereum.Signer.Crypto.BN128;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.ZkProofsVerifier.Groth16
{
    internal class Groth16VerificationKey
    {
        public ECPoint Alpha { get; set; }
        public TwistPoint Beta { get; set; }
        public TwistPoint Gamma { get; set; }
        public TwistPoint Delta { get; set; }
        public ECPoint[] IC { get; set; }
    }
}
