using System;

namespace Nethereum.ChainStateVerification
{
    public class InvalidChainDataException : Exception
    {
        public InvalidChainDataException()
        {
        }

        public InvalidChainDataException(string message) : base(message)
        {
        }

        public InvalidChainDataException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
