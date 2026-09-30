#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.TransactionSetup.Rules
{
    public sealed class Eip7702TransactionSetupRule : TransactionSetupRuleBase
    {
        private const int PER_AUTH_BASE_COST = 12500;
        private const int PER_EMPTY_ACCOUNT_COST = 25000;

        public static readonly Eip7702TransactionSetupRule Instance = new Eip7702TransactionSetupRule();

#if EVM_SYNC
        public override void ApplyAfterNonceIncrement(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.AuthorisationList == null || ctx.AuthorisationList.Count == 0)
                return;

            var authorities = Eip7702AuthorizationApplication.RequireWitnessAuthorities(ctx);

            for (int i = 0; i < ctx.AuthorisationList.Count; i++)
            {
                var auth = ctx.AuthorisationList[i];
                if (!Eip7702AuthorizationApplication.TryValidate(ctx, auth, authorities[i], out var validated))
                    continue;

                var accountExists = ctx.ExecutionState.AccountExists(validated.AuthorityAddress);
                if (accountExists)
                    ctx.AuthRefund += PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST;

                Eip7702AuthorizationApplication.Apply(ctx, auth, validated);
            }
        }

        public override void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result)
            => Eip7702AuthorizationApplication.ResolveCodeIfDelegated(ctx, chargeDelegationAccess: false);
#else
        public override async Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.AuthorisationList == null || ctx.AuthorisationList.Count == 0)
                return;

            foreach (var auth in ctx.AuthorisationList)
            {
                var validatedOrNull = await Eip7702AuthorizationApplication.TryValidateAsync(ctx, auth);
                if (validatedOrNull == null)
                    continue;
                var validated = validatedOrNull.Value;

                var accountExists = await ctx.ExecutionState.AccountExistsAsync(validated.AuthorityAddress);
                if (accountExists)
                    ctx.AuthRefund += PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST;

                Eip7702AuthorizationApplication.Apply(ctx, auth, validated);
            }
        }

        public override async Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
            => await Eip7702AuthorizationApplication.ResolveCodeIfDelegatedAsync(ctx, chargeDelegationAccess: false);
#endif
    }
}
