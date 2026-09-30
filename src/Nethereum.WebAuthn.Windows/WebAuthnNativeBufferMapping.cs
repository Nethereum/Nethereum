namespace Nethereum.WebAuthn.Windows
{
    public static class WebAuthnNativeBufferMapping
    {
        public static string OriginFor(string rpId) => "https://" + rpId;

        public static string BuildCreateClientDataJson(byte[] challenge, string origin) =>
            WebAuthnClientData.BuildCreate(challenge, origin);

        public static string BuildGetClientDataJson(byte[] challenge, string origin) =>
            WebAuthnClientData.BuildGet(challenge, origin);

        public static WebAuthnCreatedCredential MapCreatedCredential(byte[] authenticatorData, byte[] credentialId, bool requireUserVerification)
        {
            var cosePublicKey = WebAuthnResponseParser.ExtractCosePublicKeyFromAuthenticatorData(authenticatorData);
            var (x, y) = WebAuthnResponseParser.DecodeP256PublicKeyFromCose(cosePublicKey);

            return new WebAuthnCreatedCredential
            {
                PlatformCredentialId = credentialId,
                PubKeyX = x,
                PubKeyY = y,
                RequireUserVerification = requireUserVerification
            };
        }

        public static WebAuthnAssertion MapAssertion(byte[] credentialId, byte[] authenticatorData, string clientDataJson, byte[] derSignature)
        {
            var (r, s) = WebAuthnResponseParser.DecodeDerEcdsaSignatureToLowS(derSignature);

            return new WebAuthnAssertion
            {
                CredentialId = credentialId,
                AuthenticatorData = authenticatorData,
                ClientDataJSON = clientDataJson,
                R = r,
                S = s
            };
        }
    }
}
