using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// EIP-2929 (Berlin) cold/warm storage key access rule. Returns
    /// <see cref="GasConstants.COLD_SLOAD_COST"/> (2100) on the first
    /// touch of a storage key in the current contract, then
    /// <see cref="GasConstants.WARM_STORAGE_READ_COST"/> (100)
    /// thereafter. Not fork-variant Berlin → Osaka.
    /// </summary>
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
