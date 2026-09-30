using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class WebAuthnClientDataTests
    {
        private static readonly byte[] Challenge = { 1, 2, 3, 4, 5, 6, 7, 8 };
        private const string Origin = "https://nethereum.local";

        [Fact]
        public void BuildGet_matches_the_exact_reference_format()
        {
            var json = WebAuthnClientData.BuildGet(Challenge, Origin);

            Assert.Equal(
                "{\"type\":\"webauthn.get\",\"challenge\":\"" + Base64UrlEncoder.Encode(Challenge) + "\",\"origin\":\"" + Origin + "\"}",
                json);
        }

        [Fact]
        public void BuildCreate_matches_the_exact_reference_format()
        {
            var json = WebAuthnClientData.BuildCreate(Challenge, Origin);

            Assert.Equal(
                "{\"type\":\"webauthn.create\",\"challenge\":\"" + Base64UrlEncoder.Encode(Challenge) + "\",\"origin\":\"" + Origin + "\"}",
                json);
        }
    }
}
