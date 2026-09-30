using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class CreateGasCost : IOpcodeGasCost
    {
        private readonly bool _hasInitCodeWordGas;
        private readonly long _baseCost;

        public CreateGasCost(bool hasInitCodeWordGas = true, long baseCost = GasConstants.CREATE_BASE)
        {
            _hasInitCodeWordGas = hasInitCodeWordGas;
            _baseCost = baseCost;
        }

        public long GetGasCost(Program program)
        {
            var memoryIndex = program.StackPeekAtU256(1);
            var memoryLength = program.StackPeekAtU256(2);

            var memoryCost = program.CalculateMemoryExpansionGas(memoryIndex, memoryLength);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            if (!_hasInitCodeWordGas)
                return _baseCost + memoryCost;

            var words = ((memoryLength + new EvmUInt256(31)) / new EvmUInt256(32)).ToLongSafe();
            return _baseCost + (GasConstants.INIT_CODE_WORD_GAS * words) + memoryCost;
        }
    }

    public sealed class Create2GasCost : IOpcodeGasCost
    {
        private readonly bool _hasInitCodeWordGas;
        private readonly long _baseCost;

        public Create2GasCost(bool hasInitCodeWordGas = true, long baseCost = GasConstants.CREATE_BASE)
        {
            _hasInitCodeWordGas = hasInitCodeWordGas;
            _baseCost = baseCost;
        }

        public long GetGasCost(Program program)
        {
            var memoryIndex = program.StackPeekAtU256(1);
            var memoryLength = program.StackPeekAtU256(2);

            var memoryCost = program.CalculateMemoryExpansionGas(memoryIndex, memoryLength);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            var words = ((memoryLength + new EvmUInt256(31)) / new EvmUInt256(32)).ToLongSafe();
            var hashCost = GasConstants.KECCAK256_PER_WORD * words;

            if (!_hasInitCodeWordGas)
                return _baseCost + hashCost + memoryCost;

            var initCodeCost = GasConstants.INIT_CODE_WORD_GAS * words;
            return _baseCost + hashCost + initCodeCost + memoryCost;
        }
    }
}
