using Nethereum.Util;

namespace Nethereum.Model
{
    public static class BaseFeeCalculator
    {
        public const int ELASTICITY_MULTIPLIER = 2;
        public const int BASE_FEE_MAX_CHANGE_DENOMINATOR = 8;
        public const long INITIAL_BASE_FEE = 1_000_000_000;

        /// <summary>
        /// EIP-1559 <c>validate_block</c>: <c>parent_gas_target = parent.gas_limit //
        /// ELASTICITY_MULTIPLIER</c>; "on the fork block, don't account for the
        /// ELASTICITY_MULTIPLIER" - here, a parent with no declared base fee predates
        /// London and so this block IS the fork block, so
        /// <c>expected_base_fee_per_gas = INITIAL_BASE_FEE</c>. Otherwise:
        /// <c>if parent_gas_used == parent_gas_target: expected = parent_base_fee_per_gas
        /// elif parent_gas_used &gt; parent_gas_target: expected = parent_base_fee_per_gas +
        /// max(parent_base_fee_per_gas * gas_used_delta // parent_gas_target //
        /// BASE_FEE_MAX_CHANGE_DENOMINATOR, 1) else: expected = parent_base_fee_per_gas -
        /// parent_base_fee_per_gas * gas_used_delta // parent_gas_target //
        /// BASE_FEE_MAX_CHANGE_DENOMINATOR</c>. Confirmed unchanged from London through
        /// Amsterdam in ethereum/execution-specs (every fork's <c>fork.py</c> still defines
        /// <c>ELASTICITY_MULTIPLIER = 2</c> and <c>BASE_FEE_MAX_CHANGE_DENOMINATOR = 8</c>
        /// and calls the same <c>calculate_base_fee_per_gas</c>).
        /// </summary>
        public static EvmUInt256 CalculateExpectedBaseFeePerGas(
            EvmUInt256? parentBaseFeePerGas, long parentGasLimit, long parentGasUsed)
        {
            if (parentBaseFeePerGas is not EvmUInt256 parentBaseFee)
                return new EvmUInt256(INITIAL_BASE_FEE);

            var parentGasTarget = new EvmUInt256((ulong)parentGasLimit / ELASTICITY_MULTIPLIER);
            var gasUsed = new EvmUInt256((ulong)parentGasUsed);
            var denominator = new EvmUInt256(BASE_FEE_MAX_CHANGE_DENOMINATOR);

            if (gasUsed == parentGasTarget)
                return parentBaseFee;

            if (gasUsed > parentGasTarget)
            {
                var gasUsedDelta = gasUsed - parentGasTarget;
                var delta = parentBaseFee * gasUsedDelta / parentGasTarget / denominator;
                if (delta < EvmUInt256.One) delta = EvmUInt256.One;
                return parentBaseFee + delta;
            }

            var gasUsedShortfall = parentGasTarget - gasUsed;
            var decrease = parentBaseFee * gasUsedShortfall / parentGasTarget / denominator;
            return parentBaseFee - decrease;
        }
    }
}
