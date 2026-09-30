namespace Nethereum.WebAuthn
{
    public static class WebAuthnClientData
    {
        public static string BuildGet(byte[] challenge, string origin) => Build("webauthn.get", challenge, origin);

        public static string BuildCreate(byte[] challenge, string origin) => Build("webauthn.create", challenge, origin);

        private static string Build(string type, byte[] challenge, string origin) =>
            "{\"type\":\"" + type + "\",\"challenge\":\"" + Base64UrlEncoder.Encode(challenge) + "\",\"origin\":\"" + origin + "\"}";
    }
}
