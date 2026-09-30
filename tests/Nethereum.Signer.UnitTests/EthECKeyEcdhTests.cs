using System;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EthECKeyEcdhTests
    {
        [Fact]
        public void Given_TwoKeys_When_EcdhSharedPointCompressed_Then_BothSidesDeriveTheSamePoint()
        {
            var alice = EthECKey.GenerateKey();
            var bob = EthECKey.GenerateKey();

            var aliceView = alice.CalculateEcdhSharedPointCompressed(bob.GetPubKey(compresseed: true));
            var bobView = bob.CalculateEcdhSharedPointCompressed(alice.GetPubKey(compresseed: true));

            Assert.Equal(33, aliceView.Length);
            Assert.Equal(aliceView, bobView);
        }

        [Fact]
        public void Given_NullRemoteKey_When_EcdhSharedPointCompressed_Then_Throws()
        {
            var key = EthECKey.GenerateKey();
            Assert.Throws<ArgumentException>(() => key.CalculateEcdhSharedPointCompressed(null));
        }

        [Fact]
        public void Given_WrongLengthRemoteKey_When_EcdhSharedPointCompressed_Then_Throws()
        {
            var key = EthECKey.GenerateKey();
            Assert.Throws<ArgumentException>(() => key.CalculateEcdhSharedPointCompressed(new byte[32]));
            Assert.Throws<ArgumentException>(() => key.CalculateEcdhSharedPointCompressed(new byte[65]));
        }

        [Fact]
        public void Given_OffCurveRemoteKey_When_EcdhSharedPointCompressed_Then_Throws()
        {
            var key = EthECKey.GenerateKey();
            for (int attempt = 0; attempt < 256; attempt++)
            {
                var buf = new byte[33];
                buf[0] = 0x02;
                for (int i = 1; i < 33; i++) buf[i] = (byte)(attempt + i);
                try { key.CalculateEcdhSharedPointCompressed(buf); }
                catch (ArgumentException) { return; }
            }
            throw new InvalidOperationException("Could not synthesise an off-curve compressed point in 256 attempts");
        }
    }
}
