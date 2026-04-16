namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// Pure-math gas calculation strategy for a single precompile address.
    /// The calculator's identity IS the address — it lives in a specific
    /// slot of <see cref="PrecompileGasCalculators"/>, so the address
    /// parameter from the old <c>IPrecompileGasSchedule.GetGasCost</c>
    /// shape is implicit here.
    ///
    /// Implementations must be stateless and free of heap allocations on
    /// the hot path beyond what the underlying arithmetic needs. No
    /// <c>System.Numerics.BigInteger</c> — <see cref="Nethereum.Util.EvmUInt256"/>
    /// is the wide-integer primitive for anything that can exceed
    /// <c>long</c>.
    /// </summary>
    public interface IPrecompileGasCalculator
    {
        /// <summary>
        /// Returns the gas cost in wei for invoking this precompile on
        /// <paramref name="input"/>. Callers pass the raw CALL data; the
        /// calculator is responsible for parsing any length headers or
        /// rule-specific structure.
        /// </summary>
        long GetGasCost(byte[] input);
    }
}
