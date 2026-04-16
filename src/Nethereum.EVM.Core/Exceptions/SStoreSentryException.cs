using System;

namespace Nethereum.EVM.Exceptions
{
    public class SStoreSentryException : Exception
    {
        public long GasRemaining { get; }
        public long SentryRequired { get; }

        public SStoreSentryException(long gasRemaining, long sentryRequired)
            : base($"SSTORE requires at least {sentryRequired} gas for re-entrancy sentry, remaining {gasRemaining}")
        {
            GasRemaining = gasRemaining;
            SentryRequired = sentryRequired;
        }
    }
}
