using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class SstoreGasCost : IOpcodeGasCostAsync
    {
        private readonly Gas.Opcodes.Rules.IAccessStorageRule _storageRule;

        public SstoreGasCost(Gas.Opcodes.Rules.IAccessStorageRule storageRule)
        {
            _storageRule = storageRule;
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

            var isWarm = state.IsStorageKeyWarm(key);
            if (!isWarm)
            {
                state.MarkStorageKeyAsWarm(key);
            }

            long gasCost = isWarm ? 0 : GasConstants.COLD_SLOAD_COST;

#if EVM_SYNC
            var currentVal = program.ProgramContext.ExecutionStateService.GetFromStorage(contextAddress, key)?.PadTo32Bytes()
#else
            var currentVal = (await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(contextAddress, key))?.PadTo32Bytes()
#endif
                ?? ByteUtil.InitialiseEmptyByteArray(32);

            var origVal = state.OriginalStorageValues.ContainsKey(key)
                ? state.OriginalStorageValues[key]?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32)
                : currentVal;

            if (ByteUtil.AreEqual(newValue, currentVal))
            {
                return gasCost + GasConstants.SSTORE_NOOP;
            }

            if (ByteUtil.AreEqual(currentVal, origVal))
            {
                gasCost += ByteUtil.IsZero(origVal) ? GasConstants.SSTORE_SET : GasConstants.SSTORE_RESET;
            }
            else
            {
                gasCost += GasConstants.SSTORE_NOOP;
            }

            return gasCost;
        }
    }
}
