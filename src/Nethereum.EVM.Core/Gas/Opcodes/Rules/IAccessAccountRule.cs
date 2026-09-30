namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public interface IAccessAccountRule
    {
        long GetAccessCost(Program program, byte[] addressBytes);
    }
}
