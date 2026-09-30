namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public sealed class Eip2929AccessAccountRule : IAccessAccountRule
    {
        public static readonly Eip2929AccessAccountRule Instance = new Eip2929AccessAccountRule(GasConstants.COLD_ACCOUNT_ACCESS_COST);

        private readonly long _coldAccessCost;

        public Eip2929AccessAccountRule(long coldAccessCost)
        {
            _coldAccessCost = coldAccessCost;
        }

        public long GetAccessCost(Program program, byte[] addressBytes)
        {
            if (program.IsAddressWarm(addressBytes))
                return GasConstants.WARM_STORAGE_READ_COST;

            program.MarkAddressAsWarm(addressBytes);
            return _coldAccessCost;
        }
    }
}
