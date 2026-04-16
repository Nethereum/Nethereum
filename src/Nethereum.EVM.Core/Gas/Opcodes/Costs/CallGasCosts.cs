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
#if EVM_SYNC
            var isEmptyAccount = program.ProgramContext.ExecutionStateService.IsAccountEmpty(to);
#else
            var isEmptyAccount = await program.ProgramContext.ExecutionStateService.IsAccountEmptyAsync(to);
#endif

            long memCost = CallMemoryHelper.Calculate(program, inOffset, inSize, outOffset, outSize);
            long baseGas = accessCost + memCost;

            if (!value.IsZero)
            {
                baseGas += GasConstants.CALL_VALUE_TRANSFER;
            }

            // Pre-EIP-161 (Frontier-Tangerine): NEW_ACCOUNT charged whenever target doesn't exist
            // EIP-161+ (Spurious Dragon): NEW_ACCOUNT only charged when value > 0
            if (isEmptyAccount && (!_newAccountRequiresValue || !value.IsZero))
            {
                baseGas += GasConstants.CALL_NEW_ACCOUNT;
            }

            return baseGas;
        }
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
