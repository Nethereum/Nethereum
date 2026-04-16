using Nethereum.Util;

namespace Nethereum.EVM.Execution.SelfDestruct.Rules
{
    public sealed class PreCancunSelfDestructRule : ISelfDestructRule
    {
        public static readonly PreCancunSelfDestructRule WithRefund24000 = new PreCancunSelfDestructRule(24000);
        public static readonly PreCancunSelfDestructRule WithRefund0 = new PreCancunSelfDestructRule(0);

        private readonly long _refund;

        public PreCancunSelfDestructRule(long refund)
        {
            _refund = refund;
        }

        public void Execute(ref SelfDestructContext ctx)
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

            if (_refund > 0)
                ctx.Program.AddRefund(_refund);
        }
    }
}
