using System;

namespace Nethereum.EVM.Exceptions
{
    public class StaticCallViolationException : Exception
    {
        public string Operation { get; }

        public StaticCallViolationException(string operation)
            : base($"State modification not allowed in static call: {operation}")
        {
            Operation = operation;
        }

        public StaticCallViolationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
