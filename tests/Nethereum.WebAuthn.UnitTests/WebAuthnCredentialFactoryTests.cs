using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.WebAuthn.UnitTests
{
    public class WebAuthnCredentialFactoryTests
    {
        [Fact]
        public async Task Software_create_returns_its_own_key_and_derived_on_chain_id()
        {
            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: true);
            var (x, y) = authenticator.GetPublicKey();

            var created = await authenticator.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
            {
                RpId = "nethereum.local",
                UserName = "demo"
            });

            Assert.Equal(x, created.PubKeyX);
            Assert.Equal(y, created.PubKeyY);
            Assert.Equal(authenticator.CredentialId, created.PlatformCredentialId);
            Assert.True(created.RequireUserVerification);
            Assert.Equal(WebAuthnValidatorFormat.GenerateCredentialId(x, y), created.OnChainCredentialId);

            var credential = created.ToCredential();
            Assert.Equal(x, credential.PubKeyX);
            Assert.Equal(y, credential.PubKeyY);
            Assert.True(credential.RequireUV);
        }

        [Fact]
        public async Task Signer_selects_the_passkey_by_platform_handle_but_encodes_the_on_chain_id()
        {
            var recording = new RecordingAuthenticator();
            var (x, y) = recording.PublicKey;
            var onChainId = WebAuthnValidatorFormat.GenerateCredentialId(x, y);
            var platformHandle = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            var signer = new WebAuthnSignTypedDataV4(recording, onChainId, "nethereum.local", usePrecompile: false, platformCredentialId: platformHandle);

            var signatureHex = await signer.SendRequestAsync(MinimalTypedData);

            Assert.Equal(platformHandle, recording.LastCredentialId);

            var decoded = WebAuthnSignatureDecoder.Decode(signatureHex.HexToByteArray());
            Assert.Equal(onChainId, Assert.Single(decoded.CredentialIds));
        }

        private const string MinimalTypedData =
            "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"chainId\",\"type\":\"uint256\"}]," +
            "\"Mail\":[{\"name\":\"contents\",\"type\":\"string\"}]}," +
            "\"primaryType\":\"Mail\",\"domain\":{\"name\":\"Test\",\"chainId\":1},\"message\":{\"contents\":\"hi\"}}";

        private class RecordingAuthenticator : IWebAuthnAuthenticator
        {
            private readonly SoftwareWebAuthnAuthenticator _inner = new SoftwareWebAuthnAuthenticator();
            public byte[]? LastCredentialId { get; private set; }
            public (BigInteger X, BigInteger Y) PublicKey => _inner.GetPublicKey();

            public Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId)
            {
                LastCredentialId = credentialId;
                return _inner.GetAssertionAsync(challenge, credentialId, rpId);
            }
        }
    }
}
