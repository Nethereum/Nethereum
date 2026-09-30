using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
#if !EVM_SYNC
using Nethereum.Signer;
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-7702 authorization validity and application — the fork-INDEPENDENT
    /// half of authorization processing. Chain-id match, nonce-overflow,
    /// signature recovery, "existing code must be empty or already
    /// delegated", and nonce match are EIP-7702 rules that do not change
    /// between Prague, Osaka and Amsterdam — only what is CHARGED for a
    /// valid authorization does (<see cref="TransactionSetup.Rules.Eip7702TransactionSetupRule"/>'s
    /// flat refund vs <see cref="TransactionSetup.Rules.Eip8037AuthorizationApplicationRule"/>'s
    /// state-gas model). Shared here so a correction to validity is made
    /// once, following the same shared-helper precedent as
    /// <see cref="Eip7702DelegationUtils"/> rather than inheritance.
    ///
    /// <para>
    /// <b><see cref="TryValidate"/> deliberately does not mutate anything.</b>
    /// Each fork's own charging decision (NEW_ACCOUNT liveness, "already
    /// delegated" for AUTH_BASE, or the flat empty-account refund) must
    /// read state BEFORE the authority's nonce is bumped — bumping first
    /// would make every authority look "alive" (nonzero nonce) to its own
    /// liveness check immediately afterward. This is why validation and
    /// application are two calls, not one: callers read
    /// <see cref="ValidatedAuthorization.ExistingCode"/> and their own
    /// liveness check, decide what to charge, and only then call
    /// <see cref="Apply"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Fault-path handling.</b> A host/storage fault mid-authorization
    /// (missing witness data, storage IO, cancellation, OOM) must halt
    /// loudly rather than being silently treated as "this tuple is
    /// invalid, skip it" — exactly what <see cref="EvmHostException.IsHostOrSystemFault"/>
    /// exists to prevent elsewhere in this codebase (<c>EVMSimulator</c>,
    /// <c>TransactionExecutor</c>). Nothing here catches, so every read below
    /// propagates whatever it throws.
    /// </para>
    /// </summary>
    public static class Eip7702AuthorizationApplication
    {
        public readonly struct ValidatedAuthorization
        {
            public string AuthorityAddress { get; }
            public byte[] ExistingCode { get; }
            public bool IsClearing { get; }

            public ValidatedAuthorization(string authorityAddress, byte[] existingCode, bool isClearing)
            {
                AuthorityAddress = authorityAddress;
                ExistingCode = existingCode;
                IsClearing = isClearing;
            }
        }

#if EVM_SYNC
        public static List<string> RequireWitnessAuthorities(TransactionExecutionContext ctx)
        {
            var authorities = ctx.AuthorisationAuthorities;
            if (authorities != null && authorities.Count == ctx.AuthorisationList.Count)
                return authorities;

            throw new EvmHostException(
                "Witness carries " + (authorities == null ? "no" : authorities.Count.ToString()) +
                " EIP-7702 authorities for a transaction declaring " + ctx.AuthorisationList.Count +
                " authorization tuple(s). A synchronous engine cannot recover them; the witness " +
                "producer must supply one entry per tuple, null where nothing was recovered.");
        }

        public static bool TryValidate(TransactionExecutionContext ctx, Authorisation7702Signed auth, string authorityAddress, out ValidatedAuthorization validated)
        {
            validated = default;

            if (string.IsNullOrEmpty(authorityAddress))
                return false;

            if (!auth.ChainId.IsZero && auth.ChainId != ctx.ChainId)
                return false;

            if ((ulong)auth.Nonce + 1 < (ulong)auth.Nonce)
                return false;

            ctx.ExecutionState.MarkAddressAsWarm(authorityAddress);

            var existingCode = ctx.ExecutionState.GetCode(authorityAddress);
            if (existingCode != null && existingCode.Length > 0 && !Eip7702DelegationUtils.IsDelegatedCode(existingCode))
                return false;

            var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(authorityAddress);
            if (authorityAccount.Nonce == null)
                authorityAccount.Nonce = ctx.ExecutionState.StateReader.GetTransactionCount(authorityAddress);

            if ((ulong)auth.Nonce != (ulong)(authorityAccount.Nonce ?? 0UL))
                return false;

            var isClearing = AddressUtil.Current.IsNullEmptyOrZeroAddress(auth.Address);
            validated = new ValidatedAuthorization(authorityAddress, existingCode, isClearing);
            return true;
        }

        public static void ResolveCodeIfDelegated(TransactionExecutionContext ctx, bool chargeDelegationAccess)
        {
            if (ctx.IsContractCreation || string.IsNullOrEmpty(ctx.To) || ctx.Code == null)
                return;

            if (!Eip7702DelegationUtils.IsDelegatedCode(ctx.Code))
                return;

            var delegateAddress = Eip7702DelegationUtils.GetDelegateAddress(ctx.Code);
            if (!TryChargeDelegationAccessGas(ctx, delegateAddress, chargeDelegationAccess))
                return;

            ctx.ExecutionState.MarkAddressAsWarm(delegateAddress);
            ctx.Code = ctx.ExecutionState.GetCode(delegateAddress);
            ctx.DelegateAddress = delegateAddress;
        }

#else
        public static async Task<ValidatedAuthorization?> TryValidateAsync(TransactionExecutionContext ctx, Authorisation7702Signed auth)
        {
            if (!auth.ChainId.IsZero && auth.ChainId != ctx.ChainId)
                return null;

            if ((ulong)auth.Nonce + 1 < (ulong)auth.Nonce)
                return null;

            var authorityAddress = auth.TryRecoverSignerAddress();
            if (authorityAddress == null)
                return null;

            ctx.ExecutionState.MarkAddressAsWarm(authorityAddress);

            var existingCode = await ctx.ExecutionState.GetCodeAsync(authorityAddress);
            if (existingCode != null && existingCode.Length > 0 && !Eip7702DelegationUtils.IsDelegatedCode(existingCode))
                return null;

            var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(authorityAddress);
            if (authorityAccount.Nonce == null)
                authorityAccount.Nonce = await ctx.ExecutionState.StateReader.GetTransactionCountAsync(authorityAddress);

            if ((ulong)auth.Nonce != (ulong)(authorityAccount.Nonce ?? 0UL))
                return null;

            var isClearing = AddressUtil.Current.IsNullEmptyOrZeroAddress(auth.Address);
            return new ValidatedAuthorization(authorityAddress, existingCode, isClearing);
        }

        public static async Task ResolveCodeIfDelegatedAsync(TransactionExecutionContext ctx, bool chargeDelegationAccess)
        {
            if (ctx.IsContractCreation || string.IsNullOrEmpty(ctx.To) || ctx.Code == null)
                return;

            if (!Eip7702DelegationUtils.IsDelegatedCode(ctx.Code))
                return;

            var delegateAddress = Eip7702DelegationUtils.GetDelegateAddress(ctx.Code);
            if (!TryChargeDelegationAccessGas(ctx, delegateAddress, chargeDelegationAccess))
                return;

            ctx.ExecutionState.MarkAddressAsWarm(delegateAddress);
            ctx.Code = await ctx.ExecutionState.GetCodeAsync(delegateAddress);
            ctx.DelegateAddress = delegateAddress;
        }

#endif

        private static bool TryChargeDelegationAccessGas(TransactionExecutionContext ctx, string delegateAddress, bool charge)
        {
            if (!charge)
                return true;

            var wasWarm = ctx.ExecutionState.AddressIsWarm(delegateAddress);
            var accessCost = wasWarm ? Gas.GasConstants.WARM_STORAGE_READ_COST : Gas.GasConstants.EIP8038_COLD_ACCOUNT_ACCESS;
            if (ctx.PreDispatchExecutionGasAvailable < accessCost)
            {
                ctx.ExecutionState.RevertToSnapshot(ctx.PrepPhaseSnapshotId);
                ctx.CodeResolutionFailed = true;
                ctx.StateGas = new Gas.StateGasAccount { ReservoirRemaining = ctx.StateGasReservoir };
                ctx.PreDispatchExecutionGasCharged = 0;
                return false;
            }

            ctx.PreDispatchExecutionGasCharged += accessCost;
            return true;
        }

        public static void Apply(TransactionExecutionContext ctx, Authorisation7702Signed auth, ValidatedAuthorization validated)
        {
            var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(validated.AuthorityAddress);
            authorityAccount.Nonce = (ulong)auth.Nonce + 1;
            authorityAccount.Code = validated.IsClearing ? new byte[0] : Eip7702DelegationUtils.CreateDelegationCode(auth.Address);
        }
    }
}
