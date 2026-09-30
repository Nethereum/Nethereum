using System.Numerics;

namespace Nethereum.WebAuthn
{
    public class WebAuthnCreatedCredential
    {
        public byte[] PlatformCredentialId { get; set; } = System.Array.Empty<byte>();
        public BigInteger PubKeyX { get; set; }
        public BigInteger PubKeyY { get; set; }
        public bool RequireUserVerification { get; set; }

        public byte[] OnChainCredentialId => WebAuthnValidatorFormat.GenerateCredentialId(PubKeyX, PubKeyY);

        public WebAuthnCredential ToCredential() => new WebAuthnCredential(PubKeyX, PubKeyY, RequireUserVerification);
    }
}
