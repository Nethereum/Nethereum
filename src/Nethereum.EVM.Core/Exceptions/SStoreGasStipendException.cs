using System;

namespace Nethereum.EVM.Exceptions
{
    public class SStoreGasStipendException : EvmHaltException
    {
        public long GasRemaining { get; }
        public long StipendRequired { get; }

        public SStoreGasStipendException(long gasRemaining, long stipendRequired)
            : base(EvmHaltReason.OutOfGas, $"SSTORE requires more than the {stipendRequired} gas stipend, remaining {gasRemaining}")
        {
            GasRemaining = gasRemaining;
            StipendRequired = stipendRequired;
        }
    }
}
