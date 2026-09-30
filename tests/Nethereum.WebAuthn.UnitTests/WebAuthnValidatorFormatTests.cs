using System.Numerics;
using Nethereum.ABI;
using Nethereum.Util;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class WebAuthnValidatorFormatTests
    {
        [Fact]
        public void GenerateCredentialId_is_keccak_of_abi_encoded_public_key()
        {
            var x = WebAuthnReferenceVectors.Case0.PubKeyX;
            var y = WebAuthnReferenceVectors.Case0.PubKeyY;

            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(x, y);

            var expected = Sha3Keccack.Current.CalculateHash(
                new ABIEncode().GetABIEncoded(new ABIValue("uint256", x), new ABIValue("uint256", y)));

            Assert.Equal(32, credentialId.Length);
            Assert.Equal(expected, credentialId);
        }

        [Fact]
        public void GenerateCredentialId_is_deterministic_and_key_specific()
        {
            var a0 = WebAuthnValidatorFormat.GenerateCredentialId(WebAuthnReferenceVectors.Case0.PubKeyX, WebAuthnReferenceVectors.Case0.PubKeyY);
            var a1 = WebAuthnValidatorFormat.GenerateCredentialId(WebAuthnReferenceVectors.Case0.PubKeyX, WebAuthnReferenceVectors.Case0.PubKeyY);
            var b = WebAuthnValidatorFormat.GenerateCredentialId(WebAuthnReferenceVectors.Case1.PubKeyX, WebAuthnReferenceVectors.Case1.PubKeyY);

            Assert.Equal(a0, a1);
            Assert.NotEqual(a0, b);
        }

        [Fact]
        public void EncodeInstallData_sorts_credentials_ascending_regardless_of_input_order()
        {
            var credA = new WebAuthnCredential(WebAuthnReferenceVectors.Case0.PubKeyX, WebAuthnReferenceVectors.Case0.PubKeyY, requireUV: false);
            var credB = new WebAuthnCredential(WebAuthnReferenceVectors.Case1.PubKeyX, WebAuthnReferenceVectors.Case1.PubKeyY, requireUV: true);

            var forward = WebAuthnValidatorFormat.EncodeInstallData(1, new[] { credA, credB });
            var reversed = WebAuthnValidatorFormat.EncodeInstallData(1, new[] { credB, credA });

            Assert.Equal(forward, reversed);

            var decoded = WebAuthnSignatureDecoder.DecodeInstall(forward);
            Assert.Equal(BigInteger.One, decoded.Threshold);
            Assert.Equal(2, decoded.Credentials.Count);

            var id0 = new BigInteger(WebAuthnValidatorFormat.GenerateCredentialId(decoded.Credentials[0].PubKeyX, decoded.Credentials[0].PubKeyY), isUnsigned: true, isBigEndian: true);
            var id1 = new BigInteger(WebAuthnValidatorFormat.GenerateCredentialId(decoded.Credentials[1].PubKeyX, decoded.Credentials[1].PubKeyY), isUnsigned: true, isBigEndian: true);
            Assert.True(id0 < id1);
        }
    }
}
