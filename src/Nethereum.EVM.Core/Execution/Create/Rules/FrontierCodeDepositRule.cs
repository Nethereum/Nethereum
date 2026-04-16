namespace Nethereum.EVM.Execution.Create.Rules
{
    public sealed class FrontierCodeDepositRule : ICodeDepositRule
    {
        public static readonly FrontierCodeDepositRule Instance = new FrontierCodeDepositRule();

        public CodeDepositResult HandleCodeDepositOOG(CodeDepositContext context)
        {
            return new CodeDepositResult
            {
                Failed = false,
                FinalCode = new byte[0],
                FinalCodeDepositCost = 0
            };
        }
    }
}
