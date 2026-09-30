namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public sealed class FixedAccessCostRule : IAccessAccountRule
    {
        private readonly long _accessCost;

        public FixedAccessCostRule(long accessCost)
        {
            _accessCost = accessCost;
        }

        public long GetAccessCost(Program program, byte[] addressBytes) => _accessCost;
    }
}
