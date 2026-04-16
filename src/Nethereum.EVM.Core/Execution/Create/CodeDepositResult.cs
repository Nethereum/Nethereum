namespace Nethereum.EVM.Execution.Create
{
    public struct CodeDepositResult
    {
        public bool Failed;
        public byte[] FinalCode;
        public long FinalCodeDepositCost;
    }
}
