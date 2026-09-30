using System;

namespace Nethereum.EVM.Exceptions
{
    public class SStoreGasStipendException : Exception
    {
        public long GasRemaining { get; }
        public long StipendRequired { get; }

        public SStoreGasStipendException(long gasRemaining, long stipendRequired)
            : base($"SSTORE requires more than the {stipendRequired} gas stipend, remaining {gasRemaining}")
        {
            GasRemaining = gasRemaining;
            StipendRequired = stipendRequired;
        }
    }
}
