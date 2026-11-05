using System;

namespace Nethereum.EVM.Exceptions
{
    public class OutOfGasException : EvmHaltException
    {
        public long GasRequired { get; }
        public long GasRemaining { get; }

        public OutOfGasException(long gasRequired, long gasRemaining)
            : base(EvmHaltReason.OutOfGas, $"Out of gas: required {gasRequired}, remaining {gasRemaining}")
        {
            GasRequired = gasRequired;
            GasRemaining = gasRemaining;
        }

        public OutOfGasException(string message) : base(EvmHaltReason.OutOfGas, message)
        {
        }

        public OutOfGasException(string message, Exception innerException) : base(EvmHaltReason.OutOfGas, message, innerException)
        {
        }
    }
}
