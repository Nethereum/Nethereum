using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class MemoryLoadStoreGasCost : IOpcodeGasCost
    {
        public static readonly MemoryLoadStoreGasCost Instance = new MemoryLoadStoreGasCost();

        public long GetGasCost(Program program)
        {
            var offset = program.StackPeekAtU256(0);
            return 3 + program.CalculateMemoryExpansionGas(offset, new EvmUInt256(32));
        }
    }

    public sealed class MemoryStore8GasCost : IOpcodeGasCost
    {
        public static readonly MemoryStore8GasCost Instance = new MemoryStore8GasCost();

        public long GetGasCost(Program program)
        {
            var offset = program.StackPeekAtU256(0);
            return 3 + program.CalculateMemoryExpansionGas(offset, EvmUInt256.One);
        }
    }

    public sealed class ReturnRevertGasCost : IOpcodeGasCost
    {
        public static readonly ReturnRevertGasCost Instance = new ReturnRevertGasCost();

        public long GetGasCost(Program program)
        {
            var offset = program.StackPeekAtU256(0);
            var length = program.StackPeekAtU256(1);
            return program.CalculateMemoryExpansionGas(offset, length);
        }
    }
}
