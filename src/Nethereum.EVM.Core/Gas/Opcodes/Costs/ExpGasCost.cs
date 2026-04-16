namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class ExpGasCost : IOpcodeGasCost
    {
        private readonly int _byteCost;

        public ExpGasCost(int byteCost)
        {
            _byteCost = byteCost;
        }

        public long GetGasCost(Program program)
        {
            var exponentBytes = program.StackPeekAt(1);

            int bytesInExponent = 0;
            for (int i = 0; i < exponentBytes.Length; i++)
            {
                if (exponentBytes[i] != 0)
                {
                    bytesInExponent = exponentBytes.Length - i;
                    break;
                }
            }

            return GasConstants.EXP_BASE + _byteCost * bytesInExponent;
        }
    }
}
