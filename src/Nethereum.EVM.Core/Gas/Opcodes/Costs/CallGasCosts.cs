using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class CallGasCost : IOpcodeGasCostAsync
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;
        private readonly bool _newAccountRequiresValue;

        public CallGasCost(IAccessAccountRule accessRule, bool newAccountRequiresValue = true) { _accessRule = accessRule; _fixedAccessCost = -1; _newAccountRequiresValue = newAccountRequiresValue; }
        public CallGasCost(long fixedAccessCost, bool newAccountRequiresValue = true) { _fixedAccessCost = fixedAccessCost; _newAccountRequiresValue = newAccountRequiresValue; }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var toBytes = program.StackPeekAt(1);
            var value = program.StackPeekAtU256(2);
            var inOffset = program.StackPeekAtU256(3);
            var inSize = program.StackPeekAtU256(4);
            var outOffset = program.StackPeekAtU256(5);
            var outSize = program.StackPeekAtU256(6);

            var accessCost = _accessRule != null
                ? _accessRule.GetAccessCost(program, toBytes)
                : _fixedAccessCost;

            var to = toBytes.ConvertToEthereumChecksumAddress();

            long memCost = CallMemoryHelper.Calculate(program, inOffset, inSize, outOffset, outSize);
            long baseGas = accessCost + memCost;

            if (!value.IsZero)
            {
                baseGas += GasConstants.CALL_VALUE_TRANSFER;
            }

            // Pre-EIP-161 (Frontier-Tangerine): NEW_ACCOUNT charged whenever target does NOT EXIST.
            //   Mirrors go-ethereum core/vm/gas_table.go gasCall: `if !evm.StateDB.Exist(addr) ...`.
            //   A freshly-CREATEd empty contract still EXISTS (IsNewContract), so no charge.
            //   The check is intentionally local + non-materialising so it does NOT inject empty
            //   zero-account entries (e.g. precompile addresses without pre-funding) into the
            //   in-tx tracker — at pre-EIP-161 such entries would pollute the post-state trie.
            //
            // EIP-161+ (Spurious Dragon): NEW_ACCOUNT only charged when value > 0 AND target is empty.
            //   Mirrors `if transfersValue && evm.StateDB.Empty(addr) ...`. IsAccountEmpty is the
            //   existing non-materialising helper and is used as-is for this branch.
            bool chargeNewAccount;
            if (_newAccountRequiresValue)
            {
                if (value.IsZero)
                {
                    chargeNewAccount = false;
                }
                else
                {
#if EVM_SYNC
                    chargeNewAccount = program.ProgramContext.ExecutionStateService.IsAccountEmpty(to);
#else
                    chargeNewAccount = await program.ProgramContext.ExecutionStateService.IsAccountEmptyAsync(to);
#endif
                }
            }
            else
            {
#if EVM_SYNC
                chargeNewAccount = !ExistsForFrontierNewAccountCheck(program.ProgramContext.ExecutionStateService, to);
#else
                chargeNewAccount = !await ExistsForFrontierNewAccountCheckAsync(program.ProgramContext.ExecutionStateService, to);
#endif
            }

            if (chargeNewAccount)
            {
                baseGas += GasConstants.CALL_NEW_ACCOUNT;
            }

            return baseGas;
        }

#if EVM_SYNC
        /// <summary>
        /// Sync mirror of <see cref="ExistsForFrontierNewAccountCheckAsync"/>.
        /// Local to CallGasCost — does not touch the shared
        /// <c>ExecutionStateService.AccountExists</c> API so callers at later
        /// forks (EXTCODEHASH, EIP-7702) keep their existing semantics.
        /// </summary>
        private static bool ExistsForFrontierNewAccountCheck(ExecutionStateService state, string address)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

            // 1. In-tx tracker hit: a freshly CREATEd contract or any touched
            //    account with non-zero fields counts as "exists".
            if (state.AccountsState.TryGetValue(normalized, out var acct))
            {
                if (acct.WasInPreState) return true;
                if (acct.IsNewContract) return true;
                if (acct.Balance.GetTotalBalance() > 0) return true;
                if (acct.Nonce.HasValue && acct.Nonce.Value > 0) return true;
                // Geth's StateDB.Exist(addr) is structural: getStateObject(addr) != nil.
                // Once SetupCallFrame's GetCodeAsync materialises the AccountState
                // with Code = Array.Empty<byte>(), the account "exists" for
                // subsequent G_NEWACCOUNT gating, even though it remains empty
                // by every field metric. Without this, tx_e1c174e2 at EIP150
                // overcharges 25,000 gas on every CALL after the first to the
                // same empty target — the contract's repeated CALLs to address
                // arg[0]=0x...24505347 fan out to G_NEWACCOUNT × N instead of
                // × 1. Geth charges G_NEWACCOUNT only on the FIRST call that
                // materialises the target.
                if (acct.WasMaterialisedByCallFrame) return true;
                if (acct.Balance.InitialChainBalance.HasValue && acct.Balance.InitialChainBalance.Value > 0) return true;
                // IsTouched covers the precompile case where SetupCallFrame
                // materialises the entry (IsTouched=true) but GetCodeAsync
                // returns null (no bytecode in storage for built-in precompiles),
                // so Code stays null. Without this check the SECOND CALL to a
                // precompile in the same tx re-charges G_NEWACCOUNT. Mainnet
                // block 505,137 exposed this: the tx makes TWO CALLs to 0x02
                // (SHA256), canonical charges G_NEWACCOUNT only on the first,
                // we charged on both → +25,000 gas → 1.25e15 wei sender delta.
                if (acct.IsTouched) return true;
            }

            // 2. Underlying chain state — read-only. Skip fields already
            //    confirmed in-tracker to avoid redundant reads.
            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var chainBalance = state.StateReader.GetBalance(normalized);
                if (chainBalance > 0) return true;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var chainNonce = state.StateReader.GetTransactionCount(normalized);
                if (chainNonce > 0) return true;
            }
            if (acct == null || acct.Code == null)
            {
                var chainCode = state.StateReader.GetCode(normalized);
                if (chainCode != null && chainCode.Length > 0) return true;
            }

            // Final fallback: structural trie presence. Geth's StateDB.Exist(addr)
            // is `getStateObject(addr) != nil` — a pre-EIP-158 touched-empty
            // account (e.g. an IDENTITY precompile leaf persisted by a prior
            // block's value-0 CALL) reads zero on every field metric but still
            // exists structurally. Without this, every subsequent CALL into
            // such an address is over-charged G_NEWACCOUNT (25,000 gas).
            // Mainnet block 55,296 exposes this on the 0x04 precompile.
            return state.StateReader.AccountExists(normalized);
        }
#else
        /// <summary>
        /// Pre-EIP-158 "does this account exist" check used to gate the
        /// G_NEWACCOUNT (25,000 gas) CALL surcharge. Mirrors geth's
        /// <c>StateDB.Exist(addr)</c>: returns true if the account has any
        /// in-tx state (IsNewContract or non-zero fields) or any persisted
        /// chain state.
        ///
        /// Crucially this is local to <see cref="CallGasCost"/> and does NOT
        /// route through <see cref="ExecutionStateService.AccountExistsAsync"/>
        /// — the shared method materialises addresses via
        /// <c>CreateOrGetAccountExecutionState</c>, which would inject an
        /// empty zero-account entry for things like uncalled precompile
        /// addresses. At Frontier-Tangerine empty touched accounts are NOT
        /// pruned, so that injection would corrupt the post-state trie.
        /// </summary>
        private static async Task<bool> ExistsForFrontierNewAccountCheckAsync(ExecutionStateService state, string address)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

            if (state.AccountsState.TryGetValue(normalized, out var acct))
            {
                if (acct.WasInPreState) return true;
                if (acct.IsNewContract) return true;
                if (acct.Balance.GetTotalBalance() > 0) return true;
                if (acct.Nonce.HasValue && acct.Nonce.Value > 0) return true;
                // Geth's StateDB.Exist(addr) is structural: getStateObject(addr) != nil.
                // Once SetupCallFrame's GetCodeAsync materialises the AccountState
                // with Code = Array.Empty<byte>(), the account "exists" for
                // subsequent G_NEWACCOUNT gating, even though it remains empty
                // by every field metric. Without this, tx_e1c174e2 at EIP150
                // overcharges 25,000 gas on every CALL after the first to the
                // same empty target — the contract's repeated CALLs to address
                // arg[0]=0x...24505347 fan out to G_NEWACCOUNT × N instead of
                // × 1. Geth charges G_NEWACCOUNT only on the FIRST call that
                // materialises the target.
                if (acct.WasMaterialisedByCallFrame) return true;
                if (acct.Balance.InitialChainBalance.HasValue && acct.Balance.InitialChainBalance.Value > 0) return true;
                // IsTouched: see sync sibling. Mainnet block 505,137 — second
                // CALL to 0x02 (SHA256) precompile in same tx incorrectly
                // re-charges G_NEWACCOUNT because precompiles never
                // materialise Code != null.
                if (acct.IsTouched) return true;
            }

            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var chainBalance = await state.StateReader.GetBalanceAsync(normalized);
                if (chainBalance > 0) return true;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var chainNonce = await state.StateReader.GetTransactionCountAsync(normalized);
                if (chainNonce > 0) return true;
            }
            if (acct == null || acct.Code == null)
            {
                var chainCode = await state.StateReader.GetCodeAsync(normalized);
                if (chainCode != null && chainCode.Length > 0) return true;
            }

            // Final fallback: structural trie presence. See sync sibling above.
            // Mainnet block 55,296: IDENTITY (0x04) precompile leaf persisted
            // by a prior block reads zero on every field but exists structurally.
            return await state.StateReader.AccountExistsAsync(normalized);
        }
#endif
    }

    public sealed class CallCodeGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;

        public CallCodeGasCost(IAccessAccountRule accessRule) { _accessRule = accessRule; _fixedAccessCost = -1; }
        public CallCodeGasCost(long fixedAccessCost) { _fixedAccessCost = fixedAccessCost; }

        public long GetGasCost(Program program)
        {
            var toBytes = program.StackPeekAt(1);
            var value = program.StackPeekAtU256(2);
            var inOffset = program.StackPeekAtU256(3);
            var inSize = program.StackPeekAtU256(4);
            var outOffset = program.StackPeekAtU256(5);
            var outSize = program.StackPeekAtU256(6);

            var accessCost = _accessRule != null
                ? _accessRule.GetAccessCost(program, toBytes)
                : _fixedAccessCost;

            long memCost = CallMemoryHelper.Calculate(program, inOffset, inSize, outOffset, outSize);
            long baseGas = accessCost + memCost;

            if (!value.IsZero)
                baseGas += GasConstants.CALL_VALUE_TRANSFER;

            return baseGas;
        }
    }

    public sealed class DelegateCallGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;

        public DelegateCallGasCost(IAccessAccountRule accessRule) { _accessRule = accessRule; _fixedAccessCost = -1; }
        public DelegateCallGasCost(long fixedAccessCost) { _fixedAccessCost = fixedAccessCost; }

        public long GetGasCost(Program program)
        {
            var toBytes = program.StackPeekAt(1);
            var inOffset = program.StackPeekAtU256(2);
            var inSize = program.StackPeekAtU256(3);
            var outOffset = program.StackPeekAtU256(4);
            var outSize = program.StackPeekAtU256(5);

            var accessCost = _accessRule != null
                ? _accessRule.GetAccessCost(program, toBytes)
                : _fixedAccessCost;

            long memCost = CallMemoryHelper.Calculate(program, inOffset, inSize, outOffset, outSize);
            return accessCost + memCost;
        }
    }

    public sealed class StaticCallGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;

        public StaticCallGasCost(IAccessAccountRule accessRule) { _accessRule = accessRule; _fixedAccessCost = -1; }
        public StaticCallGasCost(long fixedAccessCost) { _fixedAccessCost = fixedAccessCost; }

        public long GetGasCost(Program program)
        {
            var toBytes = program.StackPeekAt(1);
            var inOffset = program.StackPeekAtU256(2);
            var inSize = program.StackPeekAtU256(3);
            var outOffset = program.StackPeekAtU256(4);
            var outSize = program.StackPeekAtU256(5);

            var accessCost = _accessRule != null
                ? _accessRule.GetAccessCost(program, toBytes)
                : _fixedAccessCost;

            long memCost = CallMemoryHelper.Calculate(program, inOffset, inSize, outOffset, outSize);
            return accessCost + memCost;
        }
    }

    internal static class CallMemoryHelper
    {
        public static long Calculate(Program program,
            EvmUInt256 inOffset, EvmUInt256 inSize,
            EvmUInt256 outOffset, EvmUInt256 outSize)
        {
            var inEnd = !inSize.IsZero ? inOffset + inSize : EvmUInt256.Zero;
            var outEnd = !outSize.IsZero ? outOffset + outSize : EvmUInt256.Zero;

            if ((!inSize.IsZero && inEnd < inOffset) || (!outSize.IsZero && outEnd < outOffset))
                return GasConstants.OVERFLOW_GAS_COST;

            var maxEnd = inEnd > outEnd ? inEnd : outEnd;
            return !maxEnd.IsZero ? program.CalculateMemoryExpansionGas(EvmUInt256.Zero, maxEnd) : 0;
        }
    }
}
