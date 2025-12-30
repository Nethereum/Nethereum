using System;
using System.IO;

namespace Nethereum.Wallet.Storage
{
    public class TokenStorageOptions
    {
        public string BaseDirectory { get; set; }

        public static TokenStorageOptions Default => new TokenStorageOptions
        {
            BaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "Wallet", "tokens")
        };
    }
}
