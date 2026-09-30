using System.Numerics;

namespace Nethereum.WebAuthn
{
    public class WebAuthnCredential
    {
        public BigInteger PubKeyX { get; set; }
        public BigInteger PubKeyY { get; set; }
        public bool RequireUV { get; set; }

        public WebAuthnCredential() { }

        public WebAuthnCredential(BigInteger pubKeyX, BigInteger pubKeyY, bool requireUV)
        {
            PubKeyX = pubKeyX;
            PubKeyY = pubKeyY;
            RequireUV = requireUV;
        }
    }
}
