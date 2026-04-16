namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class LogGasCost : IOpcodeGasCost
    {
        private readonly int _numTopics;

        public LogGasCost(int numTopics)
        {
            _numTopics = numTopics;
        }

        public long GetGasCost(Program program)
        {
            var memStart = program.StackPeekAtU256(0);
            var memLength = program.StackPeekAtU256(1);

            var memoryCost = program.CalculateMemoryExpansionGas(memStart, memLength);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            long dataGas = GasConstants.LOG_PER_BYTE * memLength.ToLongSafe();
            long topicGas = GasConstants.LOG_PER_TOPIC * _numTopics;

            return GasConstants.LOG_BASE + topicGas + dataGas + memoryCost;
        }
    }
}
