namespace Nethereum.EVM.Execution.Create
{
    public interface ICodeDepositRule
    {
        CodeDepositResult HandleCodeDepositOOG(CodeDepositContext context);
    }
}
