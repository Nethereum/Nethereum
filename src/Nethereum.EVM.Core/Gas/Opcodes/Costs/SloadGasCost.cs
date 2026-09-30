using Nethereum.EVM.Gas.Opcodes.Rules;

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class SloadGasCost : IOpcodeGasCost
    {
        private readonly IAccessStorageRule _storageRule;

        public SloadGasCost(IAccessStorageRule storageRule)
        {
            _storageRule = storageRule;
        }

        public long GetGasCost(Program program)
        {
            var key = program.StackPeekAtU256(0);
            return _storageRule.GetAccessCost(program, key);
        }
    }
}
