using System.Text;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class Base64UrlEncoderTests
    {
        [Fact]
        public void Encode_is_unpadded_and_url_safe()
        {
            var encoded = Base64UrlEncoder.Encode(new byte[] { 0xFB, 0xFF });
            Assert.Equal("-_8", encoded);
            Assert.DoesNotContain('+', encoded);
            Assert.DoesNotContain('/', encoded);
            Assert.DoesNotContain('=', encoded);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(32)]
        [InlineData(65)]
        public void Encode_then_decode_round_trips_across_all_padding_remainders(int length)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++) data[i] = (byte)(i * 7 + 3);

            var round = Base64UrlEncoder.Decode(Base64UrlEncoder.Encode(data));

            Assert.Equal(data, round);
        }

        [Fact]
        public void Decodes_the_reference_challenge_to_a_32_byte_value_and_re_encodes_identically()
        {
            var challenge = WebAuthnReferenceVectors.Case0.Challenge;

            var decoded = Base64UrlEncoder.Decode(challenge);

            Assert.Equal(32, decoded.Length);
            Assert.Equal(challenge, Base64UrlEncoder.Encode(decoded));
        }

        [Fact]
        public void Encoded_challenge_appears_verbatim_inside_the_reference_clientDataJSON()
        {
            var challengeBytes = Base64UrlEncoder.Decode(WebAuthnReferenceVectors.Case0.Challenge);

            var json = "{\"type\":\"webauthn.get\",\"challenge\":\"" + Base64UrlEncoder.Encode(challengeBytes) +
                       "\",\"origin\":\"http://localhost:8080\",\"crossOrigin\":false}";

            Assert.Equal(WebAuthnReferenceVectors.Case0.ClientDataJson, json);
        }
    }
}
