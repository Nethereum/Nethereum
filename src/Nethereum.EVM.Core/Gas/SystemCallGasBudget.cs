namespace Nethereum.EVM.Gas
{
    /// <summary>
    /// The gas one fork grants a system call, in the two dimensions
    /// EIP-8037 gives it.
    ///
    /// <para>EIP-8037: "<c>SYSTEM_CALL_GAS_LIMIT = 30_000_000 +
    /// STATE_BYTES_PER_STORAGE_SET × CPSB × SYSTEM_MAX_SSTORES_PER_CALL</c>",
    /// "The additional <c>STATE_BYTES_PER_STORAGE_SET × CPSB ×
    /// SYSTEM_MAX_SSTORES_PER_CALL</c> is placed in
    /// <c>state_gas_reservoir</c> while the rest of the system call's gas is
    /// placed in <c>gas_left</c>.", "System calls remain not subject to the
    /// [EIP-7825] <c>TX_MAX_GAS_LIMIT</c> cap, do not count against the block
    /// gas limit".</para>
    ///
    /// <para>Which fork grants which budget is stated once, on
    /// <see cref="Hardforks.HardforkSpec.SystemCallGas"/>. Selecting it there
    /// is what lets a chain state its own answer rather than edit the engine.</para>
    /// </summary>
    public sealed class SystemCallGasBudget
    {
        public static readonly SystemCallGasBudget ExecutionGasOnly =
            new SystemCallGasBudget(GasConstants.SYSTEM_CALL_EXECUTION_GAS, 0);

        public static readonly SystemCallGasBudget Eip8037 =
            new SystemCallGasBudget(
                GasConstants.SYSTEM_CALL_EXECUTION_GAS,
                GasConstants.EIP8037_STORAGE_SET_STATE_GAS * GasConstants.EIP8037_SYSTEM_MAX_SSTORES_PER_CALL);

        public SystemCallGasBudget(long executionGas, long stateGasReservoir)
        {
            ExecutionGas = executionGas;
            StateGasReservoir = stateGasReservoir;
        }

        public long ExecutionGas { get; }

        public long StateGasReservoir { get; }
    }
}
