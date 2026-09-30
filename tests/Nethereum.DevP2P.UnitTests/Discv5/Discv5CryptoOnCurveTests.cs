using System;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv5
{
    public class Discv5CryptoOnCurveTests
    {
        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_ValidCompressedPubKey_When_EcdhCompressed_Then_SharedSecretProduced()
        {
            var local = EthECKey.GenerateKey();
            var peer = EthECKey.GenerateKey();
            var peerPub = peer.GetPubKey(compresseed: true);

            var shared = Discv5Crypto.EcdhCompressed(local, peerPub);

            Assert.NotNull(shared);
            Assert.Equal(33, shared.Length);
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_NullCompressedPubKey_When_EcdhCompressed_Then_Throws()
        {
            var local = EthECKey.GenerateKey();
            Assert.Throws<ArgumentException>(() => Discv5Crypto.EcdhCompressed(local, null));
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_WrongLengthCompressedPubKey_When_EcdhCompressed_Then_Throws()
        {
            var local = EthECKey.GenerateKey();
            Assert.Throws<ArgumentException>(() =>
                Discv5Crypto.EcdhCompressed(local, new byte[32]));
            Assert.Throws<ArgumentException>(() =>
                Discv5Crypto.EcdhCompressed(local, new byte[34]));
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_OffCurveCompressedPoint_When_EcdhCompressed_Then_Throws()
        {
            var local = EthECKey.GenerateKey();
            var offCurve = FindOffCurveCompressedPoint();
            Assert.Throws<ArgumentException>(() =>
                Discv5Crypto.EcdhCompressed(local, offCurve));
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_ValidSignatureAndKey_When_VerifyIdSignature_Then_True()
        {
            var key = EthECKey.GenerateKey();
            var pub = key.GetPubKey(compresseed: true);

            var hash = new byte[32];
            for (int i = 0; i < hash.Length; i++) hash[i] = (byte)(0x10 + i);

            var sig = Discv5Crypto.SignIdSignature(key, hash);

            Assert.True(Discv5Crypto.VerifyIdSignature(sig, hash, pub));
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NIST SP 800-56A §5.6.2.3.3 public-key validation")]
        public void Given_OffCurveSignerPubKey_When_VerifyIdSignature_Then_False()
        {
            var key = EthECKey.GenerateKey();
            var hash = new byte[32];
            var sig = Discv5Crypto.SignIdSignature(key, hash);

            var offCurve33 = FindOffCurveCompressedPoint();
            Assert.False(Discv5Crypto.VerifyIdSignature(sig, hash, offCurve33));
        }

        private static byte[] FindOffCurveCompressedPoint()
        {
            for (int attempt = 0; attempt < 256; attempt++)
            {
                var buf = new byte[33];
                buf[0] = 0x02;
                for (int i = 1; i < 33; i++) buf[i] = (byte)(attempt + i);
                try
                {
                    Discv5Crypto.EcdhCompressed(EthECKey.GenerateKey(), buf);
                }
                catch (ArgumentException)
                {
                    return buf;
                }
            }
            throw new InvalidOperationException(
                "Could not synthesise an off-curve compressed point in 256 attempts");
        }
    }
}
