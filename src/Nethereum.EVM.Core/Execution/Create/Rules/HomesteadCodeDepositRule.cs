namespace Nethereum.EVM.Execution.Create.Rules
{
    public sealed class HomesteadCodeDepositRule : ICodeDepositRule
    {
        public static readonly HomesteadCodeDepositRule Instance = new HomesteadCodeDepositRule();

        public CodeDepositResult HandleCodeDepositOOG(CodeDepositContext context)
        {
            return new CodeDepositResult { Failed = true };
        }
    }
}
