using System;

namespace Nethereum.RPC.Eth.ChainValidation
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
