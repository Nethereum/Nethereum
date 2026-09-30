namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-2200: "If gasleft is less than or equal to gas stipend, fail the current call
    /// frame with 'out of gas' exception." EIP-2200 calls this the gas stipend rule;
    /// "sentry" is EIP-7928's word for it ("the GAS_CALL_STIPEND sentry").
    /// </summary>
    public abstract class SstoreGasStipendPolicy
    {
        public static readonly SstoreGasStipendPolicy Disabled = new DisabledPolicy();

        public static readonly SstoreGasStipendPolicy Eip2200Active = new Eip2200ActivePolicy();

        public abstract bool ShouldOog(long gasRemaining);

        private sealed class DisabledPolicy : SstoreGasStipendPolicy
        {
            public override bool ShouldOog(long gasRemaining) => false;
        }

        private sealed class Eip2200ActivePolicy : SstoreGasStipendPolicy
        {
            public override bool ShouldOog(long gasRemaining) => gasRemaining <= Gas.GasConstants.CALL_STIPEND;
        }
    }
}
