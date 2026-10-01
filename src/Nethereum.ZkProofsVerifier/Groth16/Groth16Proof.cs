using Nethereum.Signer.Crypto.BN128;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.ZkProofsVerifier.Groth16
{
    public class Groth16Proof
    {
        public ECPoint A { get; set; }
        public TwistPoint B { get; set; }
        public ECPoint C { get; set; }
    }
}
