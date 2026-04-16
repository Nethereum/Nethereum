using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using Nethereum.Signer;
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

            var authorities = ctx.AuthorisationAuthorities;
            if (authorities == null)
                return;

            for (int i = 0; i < ctx.AuthorisationList.Count; i++)
            {
                var auth = ctx.AuthorisationList[i];
                var authorityAddress = i < authorities.Count ? authorities[i] : null;
                if (string.IsNullOrEmpty(authorityAddress))
                    continue;

                if (!auth.ChainId.IsZero && auth.ChainId != ctx.ChainId)
                    continue;

                if ((ulong)auth.Nonce + 1 < (ulong)auth.Nonce)
                    continue;

                ctx.ExecutionState.MarkAddressAsWarm(authorityAddress);

                var existingCode = ctx.ExecutionState.GetCode(authorityAddress);
                if (existingCode != null && existingCode.Length > 0 && !Eip7702DelegationUtils.IsDelegatedCode(existingCode))
                    continue;

                var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(authorityAddress);
                if (authorityAccount.Nonce == null)
                    authorityAccount.Nonce = ctx.ExecutionState.StateReader.GetTransactionCount(authorityAddress);

                if ((ulong)auth.Nonce != (ulong)(authorityAccount.Nonce ?? 0UL))
                    continue;

                var accountExists = ctx.ExecutionState.AccountExists(authorityAddress);
                if (accountExists)
                    ctx.AuthRefund += PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST;

                authorityAccount.Nonce = (ulong)auth.Nonce + 1;

                if (string.IsNullOrEmpty(auth.Address) ||
                    AddressUtil.Current.IsZeroAddress(auth.Address))
                {
                    authorityAccount.Code = new byte[0];
                }
                else
                {
                    authorityAccount.Code = Eip7702DelegationUtils.CreateDelegationCode(auth.Address);
                }
            }

            if (!string.IsNullOrEmpty(ctx.To))
            {
                var toCode = ctx.ExecutionState.GetCode(ctx.To);
                if (Eip7702DelegationUtils.IsDelegatedCode(toCode))
                {
                    var delegateAddr = Eip7702DelegationUtils.GetDelegateAddress(toCode);
                    ctx.ExecutionState.MarkAddressAsWarm(delegateAddr);
                }
            }
        }

        public override void ApplyCodeResolution(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.IsContractCreation || string.IsNullOrEmpty(ctx.To) || ctx.Code == null)
                return;

            if (!Eip7702DelegationUtils.IsDelegatedCode(ctx.Code))
                return;

            var delegateAddress = Eip7702DelegationUtils.GetDelegateAddress(ctx.Code);
            ctx.ExecutionState.MarkAddressAsWarm(delegateAddress);
            ctx.Code = ctx.ExecutionState.GetCode(delegateAddress);
            ctx.DelegateAddress = delegateAddress;
        }
#else
        public override async Task ApplyAfterNonceIncrementAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.AuthorisationList == null || ctx.AuthorisationList.Count == 0)
                return;

            foreach (var auth in ctx.AuthorisationList)
            {
                try
                {
                    if (!auth.ChainId.IsZero && auth.ChainId != ctx.ChainId)
                        continue;

                    if ((ulong)auth.Nonce + 1 < (ulong)auth.Nonce)
                        continue;

                    // y_parity must be 0 or 1. RLP encodes scalar 0 as an empty
                    // byte string, so accept both byte[]{} and byte[]{0} as y=0.
                    var yParity = auth.V == null || auth.V.Length == 0 ? (byte)0 : auth.V[0];
                    if ((auth.V != null && auth.V.Length > 1) || yParity > 1)
                        continue;
                    var authSig = EthECDSASignatureFactory.FromSignature(auth);
                    if (!authSig.IsCanonical)
                        continue;

                    var authorityAddress = auth.RecoverSignerAddress();

                    ctx.ExecutionState.MarkAddressAsWarm(authorityAddress);

                    var existingCode = await ctx.ExecutionState.GetCodeAsync(authorityAddress);
                    if (existingCode != null && existingCode.Length > 0 && !Eip7702DelegationUtils.IsDelegatedCode(existingCode))
                        continue;

                    var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(authorityAddress);
                    if (authorityAccount.Nonce == null)
                        authorityAccount.Nonce = await ctx.ExecutionState.StateReader.GetTransactionCountAsync(authorityAddress);

                    if ((ulong)auth.Nonce != (ulong)(authorityAccount.Nonce ?? 0UL))
                        continue;

                    var accountExists = await ctx.ExecutionState.AccountExistsAsync(authorityAddress);
                    if (accountExists)
                        ctx.AuthRefund += PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST;

                    authorityAccount.Nonce = (ulong)auth.Nonce + 1;

                    if (string.IsNullOrEmpty(auth.Address) ||
                        AddressUtil.Current.IsZeroAddress(auth.Address))
                    {
                        authorityAccount.Code = new byte[0];
                    }
                    else
                    {
                        authorityAccount.Code = Eip7702DelegationUtils.CreateDelegationCode(auth.Address);
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(ctx.To))
            {
                var toCode = await ctx.ExecutionState.GetCodeAsync(ctx.To);
                if (Eip7702DelegationUtils.IsDelegatedCode(toCode))
                {
                    var delegateAddr = Eip7702DelegationUtils.GetDelegateAddress(toCode);
                    ctx.ExecutionState.MarkAddressAsWarm(delegateAddr);
                }
            }
        }

        public override async Task ApplyCodeResolutionAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (ctx.IsContractCreation || string.IsNullOrEmpty(ctx.To) || ctx.Code == null)
                return;

            if (!Eip7702DelegationUtils.IsDelegatedCode(ctx.Code))
                return;

            var delegateAddress = Eip7702DelegationUtils.GetDelegateAddress(ctx.Code);
            ctx.ExecutionState.MarkAddressAsWarm(delegateAddress);
            ctx.Code = await ctx.ExecutionState.GetCodeAsync(delegateAddress);
            ctx.DelegateAddress = delegateAddress;
        }
#endif
    }
}
