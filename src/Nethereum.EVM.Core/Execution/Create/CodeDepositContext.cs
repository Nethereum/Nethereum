namespace Nethereum.EVM.Execution.Create
{
    public struct CodeDepositContext
    {
        public byte[] Code;
        public long GasRemaining;
        public long CodeDepositCost;
    }
}
