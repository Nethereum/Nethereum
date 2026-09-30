using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public sealed class Eip2929AccessStorageRule : IAccessStorageRule
    {
        public static readonly Eip2929AccessStorageRule Instance = new Eip2929AccessStorageRule();

        public long GetAccessCost(Program program, EvmUInt256 key)
        {
            if (program.IsStorageSlotWarm(key))
                return GasConstants.WARM_STORAGE_READ_COST;

            program.MarkStorageSlotAsWarm(key);
            return GasConstants.COLD_SLOAD_COST;
        }
    }
}
