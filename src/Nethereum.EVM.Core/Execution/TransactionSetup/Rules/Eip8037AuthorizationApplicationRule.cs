using Nethereum.EVM.Gas;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.TransactionSetup.Rules
{
    public sealed class Eip8037AuthorizationApplicationRule : TransactionSetupRuleBase
    {
        public static readonly Eip8037AuthorizationApplicationRule Instance = new Eip8037AuthorizationApplicationRule();
        private Eip8037AuthorizationApplicationRule() { }

#if EVM_SYNC
        public override void ApplyAfterNonceIncrement(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.AuthorisationList == null || ctx.AuthorisationList.Count == 0)
                return;

            var authorities = Eip7702AuthorizationApplication.RequireWitnessAuthorities(ctx);

            var paidWrites = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { ctx.Sender };
            if (!ctx.Value.IsZero && !string.IsNullOrEmpty(ctx.To))
                paidWrites.Add(ctx.To);

            var delegationSetFor = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < ctx.AuthorisationList.Count; i++)
            {
                var auth = ctx.AuthorisationList[i];
                if (!Eip7702AuthorizationApplication.TryValidate(ctx, auth, authorities[i], out var validated))
                    continue;

                var authorityIsAlive = ctx.ExecutionState.AccountExists(validated.AuthorityAddress);
                if (!TryChargeStateGas(ctx, authorityIsAlive ? 0 : GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS))
                {
                    FailAuthPrep(ctx, result);
                    return;
                }

                if (!paidWrites.Contains(validated.AuthorityAddress))
                {
                    if (!TryChargeExecutionGas(ctx, GasConstants.EIP8038_ACCOUNT_WRITE))
                    {
                        FailAuthPrep(ctx, result);
                        return;
                    }
                    paidWrites.Add(validated.AuthorityAddress);
                }

                if (!validated.IsClearing)
                {
                    var delegatedBeforeTx = Eip7702DelegationUtils.IsDelegatedCode(
                        ctx.ExecutionState.StateReader.GetCode(validated.AuthorityAddress));
                    if (!delegatedBeforeTx && !delegationSetFor.Contains(validated.AuthorityAddress))
                    {
                        if (!TryChargeStateGas(ctx, GasConstants.EIP8037_AUTH_BASE_STATE_GAS))
                        {
                            FailAuthPrep(ctx, result);
                            return;
                        }
                    }
                    delegationSetFor.Add(validated.AuthorityAddress);
                }

                Eip7702AuthorizationApplication.Apply(ctx, auth, validated);
            }
        }

        public override void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result)
            => Eip7702AuthorizationApplication.ResolveCodeIfDelegated(ctx, chargeDelegationAccess: true);
#else
        public override async Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.AuthorisationList == null || ctx.AuthorisationList.Count == 0)
                return;

            var paidWrites = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { ctx.Sender };
            if (!ctx.Value.IsZero && !string.IsNullOrEmpty(ctx.To))
                paidWrites.Add(ctx.To);

            var delegationSetFor = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var auth in ctx.AuthorisationList)
            {
                var validatedOrNull = await Eip7702AuthorizationApplication.TryValidateAsync(ctx, auth);
                if (validatedOrNull == null)
                    continue;
                var validated = validatedOrNull.Value;

                var authorityIsAlive = await ctx.ExecutionState.AccountExistsAsync(validated.AuthorityAddress);
                if (!TryChargeStateGas(ctx, authorityIsAlive ? 0 : GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS))
                {
                    FailAuthPrep(ctx, result);
                    return;
                }

                if (!paidWrites.Contains(validated.AuthorityAddress))
                {
                    if (!TryChargeExecutionGas(ctx, GasConstants.EIP8038_ACCOUNT_WRITE))
                    {
                        FailAuthPrep(ctx, result);
                        return;
                    }
                    paidWrites.Add(validated.AuthorityAddress);
                }

                if (!validated.IsClearing)
                {
                    var delegatedBeforeTx = Eip7702DelegationUtils.IsDelegatedCode(
                        await ctx.ExecutionState.StateReader.GetCodeAsync(validated.AuthorityAddress));
                    if (!delegatedBeforeTx && !delegationSetFor.Contains(validated.AuthorityAddress))
                    {
                        if (!TryChargeStateGas(ctx, GasConstants.EIP8037_AUTH_BASE_STATE_GAS))
                        {
                            FailAuthPrep(ctx, result);
                            return;
                        }
                    }
                    delegationSetFor.Add(validated.AuthorityAddress);
                }

                Eip7702AuthorizationApplication.Apply(ctx, auth, validated);
            }
        }

        public override async Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
            => await Eip7702AuthorizationApplication.ResolveCodeIfDelegatedAsync(ctx, chargeDelegationAccess: true);
#endif

        private static bool TryChargeStateGas(TransactionExecutionContext ctx, long amount)
            => ctx.TryChargeStateGas(amount, out _);

        private static bool TryChargeExecutionGas(TransactionExecutionContext ctx, long amount)
        {
            if (amount <= 0) return true;

            if (ctx.PreDispatchExecutionGasAvailable < amount)
                return false;

            ctx.PreDispatchExecutionGasCharged += amount;
            return true;
        }

        private static void FailAuthPrep(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.ExecutionState.RevertToSnapshot(ctx.PrepPhaseSnapshotId);
            ctx.AuthPrepFailed = true;
            ctx.StateGas = new Gas.StateGasAccount { ReservoirRemaining = ctx.StateGasReservoir };
            ctx.PreDispatchExecutionGasCharged = 0;
        }
    }
}
