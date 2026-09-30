using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class WebAuthnReferenceVectorTests
    {
        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void Reference_signature_verifies_against_its_public_key(WebAuthnReferenceVectors.Vector v)
        {
            var verified = WebAuthnAssertionCrypto.Verify(
                v.PubKeyX, v.PubKeyY,
                v.AuthenticatorDataHex.HexToByteArray(),
                v.ClientDataJson,
                WebAuthnAssertionCrypto.To32(v.R),
                WebAuthnAssertionCrypto.To32(v.S));

            Assert.True(verified);
        }

        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void Reference_s_is_low_s(WebAuthnReferenceVectors.Vector v)
        {
            var halfOrder = BigInteger.Parse(
                "57896044605178124381348723474703786764998477612067880171211129530534256022184");

            Assert.True(v.S <= halfOrder);
        }

        [Theory]
        [MemberData(nameof(WebAuthnReferenceVectors.All), MemberType = typeof(WebAuthnReferenceVectors))]
        public void Encoded_userop_signature_carries_the_reference_type_and_challenge_indices(WebAuthnReferenceVectors.Vector v)
        {
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(v.PubKeyX, v.PubKeyY);
            var assertion = new WebAuthnAssertion
            {
                CredentialId = credentialId,
                AuthenticatorData = v.AuthenticatorDataHex.HexToByteArray(),
                ClientDataJSON = v.ClientDataJson,
                R = WebAuthnAssertionCrypto.To32(v.R),
                S = WebAuthnAssertionCrypto.To32(v.S)
            };

            var encoded = WebAuthnValidatorFormat.EncodeUserOpSignature(credentialId, usePrecompile: false, assertion);
            var decoded = WebAuthnSignatureDecoder.Decode(encoded);

            var auth = Assert.Single(decoded.Auth);
            Assert.Equal(v.TypeIndex, (int)auth.TypeIndex);
            Assert.Equal(v.ChallengeIndex, (int)auth.ChallengeIndex);
            Assert.Equal(v.ClientDataJson, auth.ClientDataJSON);
            Assert.False(decoded.UsePrecompile);
        }
    }
}
