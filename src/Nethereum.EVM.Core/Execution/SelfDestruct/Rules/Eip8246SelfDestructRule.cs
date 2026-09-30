using Nethereum.Util;

namespace Nethereum.EVM.Execution.SelfDestruct.Rules
{
    public sealed class Eip8246SelfDestructRule : ISelfDestructRule
    {
        public static readonly Eip8246SelfDestructRule Instance = new Eip8246SelfDestructRule();

        public void Execute(ref SelfDestructContext ctx)
        {
            if (!ctx.RecipientAddress.IsTheSameAddress(ctx.ContractAddress))
            {
                ctx.ExecutionStateService.DebitBalance(ctx.ContractAddress, ctx.ContractBalance);
                ctx.ExecutionStateService.CreditBalance(ctx.RecipientAddress, ctx.ContractBalance);
            }

            if (ctx.ContractAccount.IsNewContract)
            {
                ctx.Program.ProgramResult.ClearedContractAccounts.Add(ctx.ContractAddress);
            }
        }
    }
}
