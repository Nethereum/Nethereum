using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class Keccak256GasCost : IOpcodeGasCost
    {
        public static readonly Keccak256GasCost Instance = new Keccak256GasCost();

        public long GetGasCost(Program program)
        {
            var index = program.StackPeekAtU256(0);
            var length = program.StackPeekAtU256(1);

            var memoryCost = program.CalculateMemoryExpansionGas(index, length);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            var wordsU = (length + new EvmUInt256(31)) / new EvmUInt256(32);
            var lengthWords = wordsU.ToLongSafe();

            return GasConstants.KECCAK256_BASE + (GasConstants.KECCAK256_PER_WORD * lengthWords) + memoryCost;
        }
    }
}
