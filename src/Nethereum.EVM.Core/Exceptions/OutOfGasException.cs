using System;

namespace Nethereum.EVM.Exceptions
{
    public class OutOfGasException : Exception
    {
        public long GasRequired { get; }
        public long GasRemaining { get; }

        public OutOfGasException(long gasRequired, long gasRemaining)
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
