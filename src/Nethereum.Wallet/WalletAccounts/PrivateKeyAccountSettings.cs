namespace Nethereum.Wallet.WalletAccounts
{
    public sealed class PrivateKeyAccountSettings
    {
        public PrivateKeyAccountSettings(string privateKey)
        {
            PrivateKey = privateKey;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string PrivateKey { get; }

        public override string ToString()
        {
            return nameof(PrivateKeyAccountSettings);
        }
    }
}
