using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv5
{
    /// <summary>
    /// Discv5 id-signature output is canonical low-s and round-trips through
    /// <see cref="Discv5Crypto.VerifyIdSignature"/>. Regression coverage for the
    /// shared <see cref="ECDSASignature.MakeCanonical"/> low-s normalisation path.
    /// See <see href="https://eips.ethereum.org/EIPS/eip-2"/>.
    /// </summary>
    public class Discv5CryptoSignCanonicalTests
    {
        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_SignedIdSignature_When_Inspected_Then_SIsLowS()
        {
            var key = EthECKey.GenerateKey();
            var hash = new byte[32];
            for (int i = 0; i < hash.Length; i++) hash[i] = (byte)(0x10 + i);

            var sigRs = Discv5Crypto.SignIdSignature(key, hash);

            var s = new BigInteger(1, sigRs, 32, 32);
            Assert.True(s.SignValue > 0, "s must be > 0");
            Assert.True(s.CompareTo(ECKey.HALF_CURVE_ORDER) <= 0,
                "s must lie in the lower half of the curve order (EIP-2)");
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_SignedIdSignature_When_VerifiedAgainstSignerPubKey_Then_True()
        {
            var key = EthECKey.GenerateKey();
            var pub = key.GetPubKey(compresseed: true);
            var hash = new byte[32];
            for (int i = 0; i < hash.Length; i++) hash[i] = (byte)(0x20 + i);

            var sigRs = Discv5Crypto.SignIdSignature(key, hash);

            Assert.True(Discv5Crypto.VerifyIdSignature(sigRs, hash, pub));
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "EIP-2 low-s")]
        public void Given_SignedIdSignature_When_Inspected_Then_RBoundedByCurveOrder()
        {
            var key = EthECKey.GenerateKey();
            var hash = new byte[32];
            for (int i = 0; i < hash.Length; i++) hash[i] = (byte)(0x30 + i);

            var sigRs = Discv5Crypto.SignIdSignature(key, hash);

            var r = new BigInteger(1, sigRs, 0, 32);
            Assert.True(r.SignValue > 0, "r must be > 0");
            Assert.True(r.CompareTo(ECKey.CURVE_ORDER) < 0, "r must be < N");
        }
    }
}
