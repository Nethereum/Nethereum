namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class FixedGasCost : IOpcodeGasCost
    {
        public static readonly FixedGasCost Zero = new FixedGasCost(0);
        public static readonly FixedGasCost G1 = new FixedGasCost(1);
        public static readonly FixedGasCost G2 = new FixedGasCost(2);
        public static readonly FixedGasCost G3 = new FixedGasCost(3);
        public static readonly FixedGasCost G5 = new FixedGasCost(5);
        public static readonly FixedGasCost G8 = new FixedGasCost(8);
        public static readonly FixedGasCost G10 = new FixedGasCost(10);
        public static readonly FixedGasCost G20 = new FixedGasCost(20);
        public static readonly FixedGasCost G100 = new FixedGasCost(100);

        private readonly long _cost;

        public FixedGasCost(long cost)
        {
            _cost = cost;
        }

        public long GetGasCost(Program program) => _cost;
    }
}
