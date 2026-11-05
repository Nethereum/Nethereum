using System;

namespace Nethereum.EVM.Exceptions
{
    public class StaticCallViolationException : EvmHaltException
    {
        public string Operation { get; }

        public StaticCallViolationException(string operation)
            : base(EvmHaltReason.WriteInStaticContext, $"State modification not allowed in static call: {operation}")
        {
            Operation = operation;
        }

        public StaticCallViolationException(string message, Exception innerException)
            : base(EvmHaltReason.WriteInStaticContext, message, innerException)
        {
        }
    }
}
