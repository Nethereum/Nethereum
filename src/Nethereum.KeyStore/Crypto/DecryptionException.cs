using System;

namespace Nethereum.KeyStore.Crypto
{
    public class DecryptionException : Exception
    {
        internal DecryptionException(string msg) : base(msg)
        {
        }
    }
}
