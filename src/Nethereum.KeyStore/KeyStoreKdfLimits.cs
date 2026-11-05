using System;

namespace Nethereum.KeyStore
{
    public class KeyStoreKdfLimits
    {
        public const long DefaultMaxScryptMemoryBytes = 512L * 1024 * 1024;
        public const int DefaultMaxScryptP = 16;
        public const int DefaultMaxPbkdf2Iterations = 10000000;

        public static KeyStoreKdfLimits Default
        {
            get { return new KeyStoreKdfLimits(); }
        }

        public long MaxScryptMemoryBytes { get; set; } = DefaultMaxScryptMemoryBytes;
        public int MaxScryptP { get; set; } = DefaultMaxScryptP;
        public int MaxPbkdf2Iterations { get; set; } = DefaultMaxPbkdf2Iterations;
    }
}
