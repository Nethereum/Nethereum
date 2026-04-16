namespace Nethereum.EVM.Gas
{
    public interface IGasForwardingCalculator
    {
        long CalculateMaxGasToForward(long gasRemaining);
    }

    public sealed class Eip150GasForwarding : IGasForwardingCalculator
    {
        public static readonly Eip150GasForwarding Instance = new Eip150GasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining - (gasRemaining / 64);
        }
    }

    public sealed class FullGasForwarding : IGasForwardingCalculator
    {
        public static readonly FullGasForwarding Instance = new FullGasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining;
        }
    }
}
