namespace Nethereum.EVM.Gas
{
    public interface IGasForwardingCalculator
    {
        long CalculateMaxGasToForward(long gasRemaining);

        long CalculateGasForCall(long gasRemaining, long userRequestedGas);
    }

    public sealed class Eip150GasForwarding : IGasForwardingCalculator
    {
        public static readonly Eip150GasForwarding Instance = new Eip150GasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining - (gasRemaining / 64);
        }

        public long CalculateGasForCall(long gasRemaining, long userRequestedGas)
        {
            var cap = gasRemaining - (gasRemaining / 64);
            return userRequestedGas < cap ? userRequestedGas : cap;
        }
    }

    public sealed class FullGasForwarding : IGasForwardingCalculator
    {
        public static readonly FullGasForwarding Instance = new FullGasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining;
        }

        public long CalculateGasForCall(long gasRemaining, long userRequestedGas)
        {
            return userRequestedGas;
        }
    }
}
