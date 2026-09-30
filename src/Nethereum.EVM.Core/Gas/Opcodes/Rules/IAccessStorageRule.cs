using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public interface IAccessStorageRule
    {
        long GetAccessCost(Program program, EvmUInt256 key);
    }
}
