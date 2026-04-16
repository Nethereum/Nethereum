using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class AccountAccessGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;

        public AccountAccessGasCost(IAccessAccountRule accessRule)
        {
            _accessRule = accessRule;
        }

        public long GetGasCost(Program program)
        {
            var addressBytes = program.StackPeekAt(0);
            return _accessRule.GetAccessCost(program, addressBytes);
        }
    }
}
