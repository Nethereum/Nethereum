using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class CopyGasCost : IOpcodeGasCost
    {
        public static readonly CopyGasCost Instance = new CopyGasCost();

        public long GetGasCost(Program program)
        {
            var indexInMemory = program.StackPeekAtU256(0);
            var lengthDataToCopy = program.StackPeekAtU256(2);

            var memoryCost = program.CalculateMemoryExpansionGas(indexInMemory, lengthDataToCopy);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            var wordsU = (lengthDataToCopy + new EvmUInt256(31)) / new EvmUInt256(32);
            var words = wordsU.FitsInULong ? (long)wordsU.U0 : long.MaxValue / 4;

            return GasConstants.COPY_BASE + (GasConstants.COPY_PER_WORD * words) + memoryCost;
        }
    }

    public sealed class MCopyGasCost : IOpcodeGasCost
    {
        public static readonly MCopyGasCost Instance = new MCopyGasCost();

        public long GetGasCost(Program program)
        {
            var destOffset = program.StackPeekAtU256(0);
            var srcOffset = program.StackPeekAtU256(1);
            var length = program.StackPeekAtU256(2);

            var destEnd = destOffset + length;
            var srcEnd = srcOffset + length;
            var maxMemoryEnd = destEnd > srcEnd ? destEnd : srcEnd;
            long memoryCost = length.IsZero ? 0 : program.CalculateMemoryExpansionGas(EvmUInt256.Zero, maxMemoryEnd);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            var words = ((length + new EvmUInt256(31)) / new EvmUInt256(32)).ToLongSafe();

            return GasConstants.COPY_BASE + (GasConstants.COPY_PER_WORD * words) + memoryCost;
        }
    }

    public sealed class ExtCodeCopyGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;

        public ExtCodeCopyGasCost(IAccessAccountRule accessRule)
        {
            _accessRule = accessRule;
            _fixedAccessCost = -1;
        }

        public ExtCodeCopyGasCost(long fixedAccessCost)
        {
            _fixedAccessCost = fixedAccessCost;
        }

        public long GetGasCost(Program program)
        {
            var addressBytes = program.StackPeekAt(0);
            var indexInMemory = program.StackPeekAtU256(1);
            var length = program.StackPeekAtU256(3);

            var memoryCost = program.CalculateMemoryExpansionGas(indexInMemory, length);
            var accessCost = _accessRule != null
                ? _accessRule.GetAccessCost(program, addressBytes)
                : _fixedAccessCost;

            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            var words = ((length + new EvmUInt256(31)) / new EvmUInt256(32)).ToLongSafe();

            return accessCost + (GasConstants.COPY_PER_WORD * words) + memoryCost;
        }
    }
}
