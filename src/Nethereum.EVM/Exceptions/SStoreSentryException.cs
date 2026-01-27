using System;
using System.Numerics;

namespace Nethereum.EVM.Exceptions
{
    public class SStoreSentryException : Exception
    {
        public BigInteger GasRemaining { get; }
        public BigInteger SentryRequired { get; }

        public SStoreSentryException(BigInteger gasRemaining, BigInteger sentryRequired)
            : base($"SSTORE requires at least {sentryRequired} gas for re-entrancy sentry, remaining {gasRemaining}")
        {
            GasRemaining = gasRemaining;
            SentryRequired = sentryRequired;
        }
    }
}
