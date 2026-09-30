using System;
using Nethereum.Signer.Crypto;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.AccountAbstraction.Tests
{
    public class SignatureValidationTests
    {
        [Fact]
        public void OldDummySignature_HasInvalidS()
        {
            var oldDummy = "0x" + new string('f', 128) + "1c";

            var rHex = oldDummy.Substring(2, 64);
            var sHex = oldDummy.Substring(66, 64);

            var r = new BigInteger(rHex, 16);
            var s = new BigInteger(sHex, 16);

            var sig = new ECDSASignature(r, s);

            Assert.False(sig.IsLowS, "Old dummy signature has invalid S value (too high)");
            Assert.False(sig.IsCanonical, "Old dummy signature is not canonical");
        }

        [Fact]
        public void NewDummySignature_HasValidS()
        {
            var newDummy = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1c";

            var rHex = newDummy.Substring(2, 64);
            var sHex = newDummy.Substring(66, 64);

            var r = new BigInteger(rHex, 16);
            var s = new BigInteger(sHex, 16);

            var sig = new ECDSASignature(r, s);

            Assert.True(sig.IsLowS, "New dummy signature has valid S value (low)");
            Assert.True(sig.IsCanonical, "New dummy signature is canonical");
        }
    }
}
