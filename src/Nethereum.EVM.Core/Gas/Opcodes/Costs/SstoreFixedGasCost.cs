using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// Pre-Berlin SSTORE gas cost (EIP-2200 model, no warm/cold).
    /// Istanbul: SET=20000, RESET=5000, NOOP=800 (SLOAD_GAS).
    /// Pre-Istanbul (Frontier→Constantinople): SET=20000, RESET=5000, no NOOP distinction.
    /// </summary>
    public sealed class SstoreFixedGasCost : IOpcodeGasCostAsync
    {
        private readonly long _sloadGas;

        public SstoreFixedGasCost(long sloadGas)
        {
            _sloadGas = sloadGas;
        }

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

            // No warm/cold tracking pre-Berlin
            state.MarkStorageKeyAsWarm(key);

#if EVM_SYNC
            var currentVal = program.ProgramContext.ExecutionStateService.GetFromStorage(contextAddress, key)?.PadTo32Bytes()
#else
            var currentVal = (await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(contextAddress, key))?.PadTo32Bytes()
#endif
                ?? ByteUtil.InitialiseEmptyByteArray(32);

            var origVal = state.OriginalStorageValues.ContainsKey(key)
                ? state.OriginalStorageValues[key]?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32)
                : currentVal;

            // EIP-2200 cost model (Istanbul)
            if (ByteUtil.AreEqual(newValue, currentVal))
            {
                return _sloadGas; // NOOP = SLOAD_GAS (800 at Istanbul, 200 at Tangerine, 50 at Frontier)
            }

            if (ByteUtil.AreEqual(currentVal, origVal))
            {
                return ByteUtil.IsZero(origVal) ? GasConstants.SSTORE_SET : GasConstants.SSTORE_RESET_PRE_BERLIN;
            }

            return _sloadGas; // Dirty slot = SLOAD_GAS
        }
    }
}
