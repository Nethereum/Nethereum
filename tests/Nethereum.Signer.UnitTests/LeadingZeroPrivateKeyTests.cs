using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    [Collection("SignRecoverableBackendMutation")]
    public class LeadingZeroPrivateKeyTests
    {
        private const string LeadingZeroKey = "0x00d3f5a2b4c6e8091a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f7081";

        [Fact]
        public void Given_APrivateKeyWithALeadingZeroByte_When_ReadAsBytes_Then_ItIsThe32ByteBigEndianScalar()
        {
            var key = new EthECKey(LeadingZeroKey);

            Assert.Equal(32, key.GetPrivateKeyAsBytes().Length);
            Assert.Equal(LeadingZeroKey, key.GetPrivateKey());
        }

        [Fact]
        public void Given_APrivateKeyWithALeadingZeroByte_When_SignedOnEitherBackend_Then_TheSignatureRecoversItsAddress()
        {
            var original = EthECKey.SignRecoverable;
            try
            {
                var key = new EthECKey(LeadingZeroKey);
                var hash = Sha3Keccack.Current.CalculateHash("nethereum".ToHexUTF8().HexToByteArray());

                foreach (var recoverable in new[] { true, false })
                {
                    EthECKey.SignRecoverable = recoverable;
                    var signature = key.SignAndCalculateV(hash);
                    var recovered = EthECKey.RecoverFromSignature(signature, hash);
                    Assert.Equal(key.GetPublicAddress(), recovered.GetPublicAddress());
                }
            }
            finally { EthECKey.SignRecoverable = original; }
        }
    }
}
