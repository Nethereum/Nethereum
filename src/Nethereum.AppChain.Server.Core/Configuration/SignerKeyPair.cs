using Nethereum.Signer;

namespace Nethereum.AppChain.Server.Configuration
{
    public sealed class SignerKeyPair
    {
        public string? PrivateKey { get; set; }

        public string? Address { get; set; }

        public bool CanSign => !string.IsNullOrEmpty(PrivateKey);

        public bool IsIdentified => !string.IsNullOrEmpty(Address);

        public void DeriveAddressFromPrivateKey()
        {
            if (CanSign && !IsIdentified)
                Address = new EthECKey(PrivateKey).GetPublicAddress();
        }
    }
}
