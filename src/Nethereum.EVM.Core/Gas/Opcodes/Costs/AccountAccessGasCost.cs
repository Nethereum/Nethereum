using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class AccountAccessGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _readSurcharge;

        public AccountAccessGasCost(IAccessAccountRule accessRule, long readSurcharge = 0)
        {
            _accessRule = accessRule;
            _readSurcharge = readSurcharge;
        }

        public long GetGasCost(Program program)
        {
            var addressBytes = program.StackPeekAt(0);
            return _accessRule.GetAccessCost(program, addressBytes) + _readSurcharge;
        }
    }
}
