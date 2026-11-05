using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// CALL's gas at the three forks before EIP-161 - Frontier, Homestead and
    /// Tangerine Whistle. EIP-161 states the rule it replaces in the course of
    /// replacing it: "<c>CALL</c> and <c>SUICIDE</c> would charge 25,000 gas
    /// when the destination is non-existent". Non-existent, not empty, and not
    /// conditional on what the call carries: an account record that exists but
    /// holds nothing still counts as existing here, and a call carrying no
    /// value is charged just the same.
    ///
    /// <para><b>This class is a half-migration.</b> Spurious Dragon onward is
    /// priced by <see cref="CallTargetResolvingGasCost"/> over a rule that
    /// holds no state reader; these three forks are not, so the existence walk
    /// below is still written twice, once per engine, and nothing in this
    /// repository compiles both arms. Its resolver is owed, and is blocked on
    /// an instrument that can see the pre-EIP-161 path at all.</para>
    /// </summary>
    public sealed class CallGasCost : IOpcodeGasCostAsync
    {
        private readonly long _fixedAccessCost;

        public CallGasCost(long fixedAccessCost)
        {
            _fixedAccessCost = fixedAccessCost;
        }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var toBytes = program.StackPeekAt(1);
            var value = program.StackPeekAtU256(2);
            var toAddress = EvmAddress.From(toBytes);

            long stateIndependentGas = StateIndependentGas(program, value);

            program.RequireGas(stateIndependentGas);
#if EVM_SYNC
            if (program.HasExecutionError) return 0;
#endif

            CallTargetRecorder.Record(program, toAddress);

            var state = program.ProgramContext.ExecutionStateService;
#if EVM_SYNC
            if (PreEip161AccountExistence.Exists(state, toAddress)) return stateIndependentGas;
#else
            if (await PreEip161AccountExistence.ExistsAsync(state, toAddress)) return stateIndependentGas;
#endif

            return stateIndependentGas + GasConstants.CALL_NEW_ACCOUNT;
        }

        /// <summary>
        /// EIP-7928, Gas Validation Before State Access: "Gas costs determinable without
        /// state access (memory expansion, base opcode cost, warm/cold access cost)".
        /// </summary>
        private long StateIndependentGas(Program program, EvmUInt256 value)
        {
            long memCost = CallMemoryHelper.Calculate(
                program,
                program.StackPeekAtU256(3), program.StackPeekAtU256(4),
                program.StackPeekAtU256(5), program.StackPeekAtU256(6));

            return _fixedAccessCost + memCost + (value.IsZero ? 0 : GasConstants.CALL_VALUE_TRANSFER);
        }
    }

    public sealed class CallCodeGasCost : IOpcodeGasCost
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly long _fixedAccessCost;
        private readonly long _valueTransferCost;

        public CallCodeGasCost(IAccessAccountRule accessRule, long valueTransferCost = GasConstants.CALL_VALUE_TRANSFER)
        {
            _accessRule = accessRule;
            _fixedAccessCost = -1;
            _valueTransferCost = valueTransferCost;
        }

        public CallCodeGasCost(long fixedAccessCost, long valueTransferCost = GasConstants.CALL_VALUE_TRANSFER)
        {
            _fixedAccessCost = fixedAccessCost;
            _valueTransferCost = valueTransferCost;
        }

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
                baseGas += _valueTransferCost;

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

    /// <summary>
    /// EIP-7928: the block access list MUST include "Targets of <c>CALL</c>,
    /// <c>CALLCODE</c>, <c>DELEGATECALL</c>, <c>STATICCALL</c> (even if they revert;
    /// see Gas Validation Before State Access for inclusion conditions)". That section
    /// supplies the condition met at the call site: "Once pre-state validation passes,
    /// the target is accessed and included in the BAL. Post-state costs are then
    /// calculated; their order is implementation-defined since the target has already
    /// been accessed."
    ///
    /// <para>Shared because the CALL family is mid-migration: the EIP-161 forks price
    /// through <see cref="CallTargetResolvingGasCost"/> and the three older ones still
    /// through <see cref="CallGasCost"/>. One copy of the condition, so the two cannot
    /// come to disagree about what enters the list.</para>
    /// </summary>
    internal static class CallTargetRecorder
    {
        public static void Record(Program program, EvmAddress to)
        {
            program.ProgramContext.ExecutionStateService.NotifyAccountRead(to);
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
