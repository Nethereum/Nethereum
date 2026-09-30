using System.Security.Cryptography;
using System.Text;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class SoftwareWebAuthnAuthenticatorTests
    {
        private const string RpId = "nethereum.local";

        private static byte[] NewChallenge(byte seed)
        {
            var challenge = new byte[32];
            for (var i = 0; i < 32; i++) challenge[i] = (byte)(seed + i);
            return challenge;
        }

        [Fact]
        public async Task Produces_an_assertion_that_verifies_against_its_own_public_key()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(origin: "https://nethereum.local");
            var (x, y) = authenticator.GetPublicKey();
            var challenge = NewChallenge(1);

            var assertion = await authenticator.GetAssertionAsync(challenge, authenticator.CredentialId, RpId);

            Assert.True(WebAuthnAssertionCrypto.Verify(x, y, assertion.AuthenticatorData, assertion.ClientDataJSON, assertion.R, assertion.S));
        }

        [Fact]
        public async Task Signature_is_always_low_s()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator();
            var halfOrder = System.Numerics.BigInteger.Parse(
                "57896044605178124381348723474703786764998477612067880171211129530534256022184");

            for (byte i = 0; i < 16; i++)
            {
                var assertion = await authenticator.GetAssertionAsync(NewChallenge(i), authenticator.CredentialId, RpId);
                var s = new System.Numerics.BigInteger(assertion.S, isUnsigned: true, isBigEndian: true);
                Assert.True(s <= halfOrder, $"s must be <= n/2 (iteration {i})");
            }
        }

        [Fact]
        public async Task AuthenticatorData_has_the_rpIdHash_flags_and_signcount_layout()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: false);

            var assertion = await authenticator.GetAssertionAsync(NewChallenge(0), authenticator.CredentialId, RpId);

            Assert.Equal(37, assertion.AuthenticatorData.Length);
            Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(RpId)), assertion.AuthenticatorData[..32]);
            Assert.Equal(0x01, assertion.AuthenticatorData[32] & 0x01);
            Assert.Equal(0x00, assertion.AuthenticatorData[32] & 0x04);
        }

        [Fact]
        public async Task RequireUV_sets_the_user_verified_flag()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: true);

            var assertion = await authenticator.GetAssertionAsync(NewChallenge(0), authenticator.CredentialId, RpId);

            Assert.Equal(0x04, assertion.AuthenticatorData[32] & 0x04);
        }

        [Fact]
        public async Task Sign_count_increments_across_assertions()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator();

            var first = await authenticator.GetAssertionAsync(NewChallenge(0), authenticator.CredentialId, RpId);
            var second = await authenticator.GetAssertionAsync(NewChallenge(1), authenticator.CredentialId, RpId);

            var firstCount = ReadSignCount(first.AuthenticatorData);
            var secondCount = ReadSignCount(second.AuthenticatorData);

            Assert.Equal(firstCount + 1, secondCount);
        }

        [Fact]
        public async Task ClientDataJSON_embeds_the_base64url_challenge_and_the_get_type()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator();
            var challenge = NewChallenge(42);

            var assertion = await authenticator.GetAssertionAsync(challenge, authenticator.CredentialId, RpId);

            Assert.Contains("\"type\":\"webauthn.get\"", assertion.ClientDataJSON);
            Assert.Contains("\"challenge\":\"" + Base64UrlEncoder.Encode(challenge) + "\"", assertion.ClientDataJSON);
        }

        [Fact]
        public async Task Assertion_round_trips_through_the_userop_signature_encoding()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator();
            var (x, y) = authenticator.GetPublicKey();
            var credentialId = WebAuthnValidatorFormat.GenerateCredentialId(x, y);

            var assertion = await authenticator.GetAssertionAsync(NewChallenge(7), credentialId, RpId);
            var encoded = WebAuthnValidatorFormat.EncodeUserOpSignature(credentialId, usePrecompile: true, assertion);

            var decoded = WebAuthnSignatureDecoder.Decode(encoded);
            var auth = Assert.Single(decoded.Auth);

            Assert.True(decoded.UsePrecompile);
            Assert.Equal(credentialId, Assert.Single(decoded.CredentialIds));
            Assert.Equal(assertion.ClientDataJSON, auth.ClientDataJSON);
            Assert.Equal(1, (int)auth.TypeIndex);
            Assert.Equal(assertion.ClientDataJSON.IndexOf("\"challenge\":\"", System.StringComparison.Ordinal), (int)auth.ChallengeIndex);
        }

        private static uint ReadSignCount(byte[] authenticatorData)
        {
            var slice = authenticatorData[33..37];
            if (System.BitConverter.IsLittleEndian) System.Array.Reverse(slice);
            return System.BitConverter.ToUInt32(slice, 0);
        }
    }
}
