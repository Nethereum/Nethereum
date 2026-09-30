namespace Nethereum.EVM.Gas
{
    public static class StateGasMeter
    {
        public static bool TryChargeStateGas(Program program, long amount, out long spilled)
        {
            spilled = 0;
            if (amount <= 0) return true;

            if (program.StateGasLeft >= amount)
            {
                program.StateGasLeft -= amount;
                return true;
            }

            var remainder = amount - program.StateGasLeft;
            if (program.GasRemaining < remainder) return false;

            program.StateGasLeft = 0;
            program.GasRemaining -= remainder;
            program.TotalGasUsed += remainder;
            program.StateGasSpilled += remainder;
            spilled = remainder;
            return true;
        }

        public static void ChargeStateGas(Program program, long amount)
        {
            if (amount <= 0) return;

            if (program.StateGasLeft >= amount)
            {
                program.StateGasLeft -= amount;
                return;
            }

            var remainder = amount - program.StateGasLeft;
            program.StateGasLeft = 0;
            program.UpdateGasUsed(remainder);
#if EVM_SYNC
            if (program.HasExecutionError) return;
#endif
            program.StateGasSpilled += remainder;
        }

        public static bool ChargeNewAccountStateGas(Program program, long amount)
        {
            if (amount <= 0 || !program.ProgramContext.StateGasActive) return false;

            ChargeStateGas(program, amount);
            return true;
        }

        /// <summary>
        /// EIP-8037 §Integration with EIP-7778: <i>"State-gas refills are not refunds in this
        /// sense…"</i> — this credit is uncapped and stays netted into <c>evm_state_gas_used</c>,
        /// unlike <see cref="Program.RefundCounter"/>, which is capped at 1/5 of gas used and
        /// excluded from EIP-7778 block accounting. Neither this method nor its callers may touch
        /// <see cref="Program.RefundCounter"/>.
        /// </summary>
        public static void CreditStateGasRefund(Program program, long amount)
        {
            if (amount <= 0) return;

            var fromGasLeft = amount < program.StateGasSpilled ? amount : program.StateGasSpilled;
            if (fromGasLeft > 0)
            {
                program.GasRemaining += fromGasLeft;
                program.TotalGasUsed -= fromGasLeft;
                program.StateGasSpilled -= fromGasLeft;
            }
            program.StateGasLeft += amount - fromGasLeft;
        }

        public static void RestoreStateGas(Program program)
        {
            if (program.StateGasSpilled != 0)
            {
                program.GasRemaining += program.StateGasSpilled;
                program.TotalGasUsed -= program.StateGasSpilled;
                program.StateGasSpilled = 0;
            }
            program.StateGasLeft = program.StateGasBaseline;

            if (program.IsExceptionalHalt)
            {
                program.GasRemaining = 0;
            }
        }

        public static long DrainReservoir(Program program)
        {
            var reservoir = program.StateGasLeft;
            program.StateGasLeft = 0;
            return reservoir;
        }

        public static void AbsorbChild(Program parent, Program child)
        {
            parent.StateGasLeft += child.StateGasLeft;
            parent.StateGasSpilled += child.StateGasSpilled;
        }

        /// <summary>
        /// EIP-8037 §Gas accounting for halts and reverts: <i>"Then, with the child's
        /// <c>state_gas_reservoir</c> merged in, the frame returns state-gas to <c>gas_left</c>,
        /// up to the amount it has outstanding:"</i>
        /// <code>
        /// d = min(state_gas_reservoir, state_gas_from_gas_left)
        /// gas_left += d
        /// state_gas_reservoir -= d
        /// state_gas_from_gas_left -= d
        /// </code>
        /// <i>"This step is needed because a refill may happen in a different frame than the
        /// matching charge."</i> and <i>"Note: this undoes no state creation, so
        /// <c>evm_state_gas_used</c> is unchanged. It only moves gas between the two pools."</i>
        ///
        /// <para>Called on the successful-child path only: <i>"The step applies to successful
        /// children only. This requires that a frame that reverts or halts exceptionally returns
        /// no state-gas to its parent; the restore to the frame's baseline above provides
        /// that."</i></para>
        /// </summary>
        public static void ReturnOutstandingStateGasToGasLeft(Program program)
        {
            var returned = program.StateGasLeft < program.StateGasSpilled
                ? program.StateGasLeft
                : program.StateGasSpilled;
            if (returned <= 0) return;

            program.GasRemaining += returned;
            program.TotalGasUsed -= returned;
            program.StateGasLeft -= returned;
            program.StateGasSpilled -= returned;
        }
    }
}
