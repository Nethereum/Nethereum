namespace Nethereum.WebAuthn
{
    public class WebAuthnAssertion
    {
        public byte[] CredentialId { get; set; }
        public byte[] AuthenticatorData { get; set; }
        public string ClientDataJSON { get; set; }
        public byte[] R { get; set; }
        public byte[] S { get; set; }
    }
}
