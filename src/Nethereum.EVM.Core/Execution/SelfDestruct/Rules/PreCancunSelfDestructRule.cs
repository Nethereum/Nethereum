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

            // Geth refunds SELFDESTRUCT once per (tx, contract). Check the
            // tx-wide self-destructed set on ExecutionStateService — frame-
            // local ProgramResult.DeletedContractAccounts misses the case
            // where the same contract is SELFDESTRUCTed via two separate
            // CALL frames in the same tx (each frame's list starts empty,
            // so refund would fire twice).
            // Matches the canonical self-destruct refund rule: only add the
            // refund if the contract has not already self-destructed in this tx.
            bool alreadyDestructed = ctx.ExecutionStateService.HasSelfDestructed(ctx.ContractAddress);
            if (!alreadyDestructed)
            {
                ctx.ExecutionStateService.MarkSelfDestructed(ctx.ContractAddress);
                ctx.Program.ProgramResult.DeletedContractAccounts.Add(ctx.ContractAddress);
                if (_refund > 0)
                    ctx.Program.AddRefund(_refund);
            }
        }
    }
}
