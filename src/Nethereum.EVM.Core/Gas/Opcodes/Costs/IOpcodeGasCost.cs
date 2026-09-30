namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public interface IOpcodeGasCost
    {
        long GetGasCost(Program program);
    }
}
