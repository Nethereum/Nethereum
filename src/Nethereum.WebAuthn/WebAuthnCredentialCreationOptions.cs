namespace Nethereum.WebAuthn
{
    public class WebAuthnCredentialCreationOptions
    {
        public string RpId { get; set; } = "";

        public string RpName { get; set; } = "";

        public string UserName { get; set; } = "";

        public byte[]? UserId { get; set; }

        public bool RequireUserVerification { get; set; }

        public byte[]? Challenge { get; set; }
    }
}
