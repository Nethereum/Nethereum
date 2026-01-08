using System;
using System.Numerics;

namespace Nethereum.EVM.Exceptions
{
    public class OutOfGasException : Exception
    {
        public BigInteger GasRequired { get; }
        public BigInteger GasRemaining { get; }

        public OutOfGasException(BigInteger gasRequired, BigInteger gasRemaining)
            : base($"Out of gas: required {gasRequired}, remaining {gasRemaining}")
        {
            GasRequired = gasRequired;
            GasRemaining = gasRemaining;
        }

        public OutOfGasException(string message) : base(message)
        {
        }

        public OutOfGasException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
