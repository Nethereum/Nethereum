using Nethereum.Util;

namespace Nethereum.EVM.Execution.SelfDestruct.Rules
{
    public sealed class Eip6780SelfDestructRule : ISelfDestructRule
    {
        public static readonly Eip6780SelfDestructRule Instance = new Eip6780SelfDestructRule();

        public void Execute(ref SelfDestructContext ctx)
        {
            if (ctx.ContractAccount.IsNewContract)
            {
                if (!ctx.RecipientAddress.IsTheSameAddress(ctx.ContractAddress))
                {
                    ctx.ExecutionStateService.DebitBalance(ctx.ContractAddress, ctx.ContractBalance);
                    ctx.ExecutionStateService.CreditBalance(ctx.RecipientAddress, ctx.ContractBalance);
                }
                else
                {
                    ctx.ExecutionStateService.DebitBalance(ctx.ContractAddress, ctx.ContractBalance);
                }
                ctx.Program.ProgramResult.DeletedContractAccounts.Add(ctx.ContractAddress);
            }
            else
            {
                if (!ctx.RecipientAddress.IsTheSameAddress(ctx.ContractAddress))
                {
                    ctx.ExecutionStateService.DebitBalance(ctx.ContractAddress, ctx.ContractBalance);
                    ctx.ExecutionStateService.CreditBalance(ctx.RecipientAddress, ctx.ContractBalance);
                }
            }
        }
    }
}
