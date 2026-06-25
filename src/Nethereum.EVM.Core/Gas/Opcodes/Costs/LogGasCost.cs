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

            // The memory-size calculation returns overflow when the length
            // does not fit in a uint64 — the LOG memory size cannot fit
            // even a uint64. randomStatetest303.json exercises this with
            // sign-extended calldata where memLength ≈ 2^254. Without this
            // guard our CalculateMemoryExpansionGas rounds the operands
            // through Int paths that come back with a finite (but wrong)
            // cost and the opcode silently overcharges instead of OOGing.
            if (!memLength.FitsInULong) return GasConstants.OVERFLOW_GAS_COST;

            var memoryCost = program.CalculateMemoryExpansionGas(memStart, memLength);
            if (memoryCost >= GasConstants.OVERFLOW_GAS_COST) return GasConstants.OVERFLOW_GAS_COST;

            long dataGas = GasConstants.LOG_PER_BYTE * memLength.ToLongSafe();
            long topicGas = GasConstants.LOG_PER_TOPIC * _numTopics;

            return GasConstants.LOG_BASE + topicGas + dataGas + memoryCost;
        }
    }
}
