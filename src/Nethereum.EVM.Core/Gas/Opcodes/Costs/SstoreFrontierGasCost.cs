using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// Pre-EIP-2200 SSTORE gas cost (Frontier through Constantinople).
    /// Simple model: SET=20000 (zero to non-zero), RESET=5000 (everything else).
    /// No NOOP concept, no warm/cold distinction.
    /// </summary>
    public sealed class SstoreFrontierGasCost : IOpcodeGasCostAsync
    {
#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var key = program.StackPeekAtU256(0);
            var newValue = program.StackPeekAt(1).PadTo32Bytes();

            var contextAddress = program.ProgramContext.AddressContract;
            var state = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(contextAddress);
            state.MarkStorageKeyAsWarm(key);

#if EVM_SYNC
            var currentVal = program.ProgramContext.ExecutionStateService.GetFromStorage(contextAddress, key)?.PadTo32Bytes()
#else
            var currentVal = (await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(contextAddress, key))?.PadTo32Bytes()
#endif
                ?? ByteUtil.InitialiseEmptyByteArray(32);

            if (ByteUtil.IsZero(currentVal) && !ByteUtil.IsZero(newValue))
            {
                return GasConstants.SSTORE_SET; // 20000
            }

            return GasConstants.SSTORE_RESET_PRE_BERLIN; // 5000
        }
    }
}
