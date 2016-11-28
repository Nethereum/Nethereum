using System;

namespace Nethereum.KeyStore
{
    public class InvalidKdfException : Exception
    {
        public InvalidKdfException(string kdf) : base("Invalid kdf:" + kdf)
        {
        }
    }
}