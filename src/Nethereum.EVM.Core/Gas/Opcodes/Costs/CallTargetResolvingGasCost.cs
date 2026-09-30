using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// The CALL entry in the opcode gas table from Spurious Dragon onward. It
    /// reads the operands, prices what the EIP calls pre-state validation,
    /// checks the frame can afford it, records the target, and hands the
    /// transfer to the rule, which prices it without touching state.
    ///
    /// <para>EIP-7928: "Pre-state validation MUST pass before any state access
    /// occurs. If pre-state validation fails, the target resource (address or
    /// storage slot) is never accessed and MUST NOT be included in the BAL."
    /// The affordability check sits between the two phases, so nothing after a
    /// refusal reaches the target. The target is still marked warm ahead of
    /// that check: EIP-2929 makes the surcharge and the entry into
    /// <c>accessed_addresses</c> one step, and the charge being refused does
    /// not undo it.</para>
    ///
    /// <para>The target is the read that is <i>not</i> unconditional, and that
    /// is the whole reason the rule is asked twice. EIP-161 and EELS alike
    /// stop before it for a call carrying no value, which is the overwhelming
    /// majority of calls, so the rule is asked first from the value alone and
    /// asked again only if it says it cannot answer yet. The read goes through
    /// <c>IsAccountEmpty</c>, which answers without materialising an entry and
    /// without notifying the block-access-list recorder; pricing therefore
    /// never injects an empty zero-account - an uncalled precompile address,
    /// say - into the in-transaction tracker.</para>
    ///
    /// <para>One body serves both engines. Two things differ and both are
    /// split where they occur: the target read, which the async host awaits
    /// against a reader that may be recording the witness as it goes while
    /// <c>EVM_SYNC</c> resolves from the pre-state it was given; and the halt
    /// line, which <c>EVM_SYNC</c> needs because an unaffordable charge sets a
    /// flag there rather than throwing.</para>
    /// </summary>
    public sealed class CallTargetResolvingGasCost : IOpcodeGasCostAsync
    {
        private readonly IAccessAccountRule _accessRule;
        private readonly ICallNewAccountRule _rule;

        public CallTargetResolvingGasCost(IAccessAccountRule accessRule, ICallNewAccountRule rule)
        {
            _accessRule = accessRule;
            _rule = rule;
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

            var preStateGas = PreStateValidationGas(program, toBytes, value);

            program.RequireGas(preStateGas);
#if EVM_SYNC
            if (program.HasExecutionError) return 0;
#endif

            CallTargetRecorder.Record(program, toAddress.ToHexLower());

            var transfer = CallTransfer.OfTheValueAlone(value);
            var verdict = _rule.JudgeTheTransfer(transfer);
            if (verdict == CallNewAccountVerdict.NotUntilTheTargetDeadnessIsKnown)
            {
                var state = program.ProgramContext.ExecutionStateService;
#if EVM_SYNC
                var targetIsDead = state.IsAccountEmpty(toAddress);
#else
                var targetIsDead = await state.IsAccountEmptyAsync(toAddress);
#endif
                verdict = _rule.JudgeTheTransfer(transfer.AndTheTargetDeadnessPerEip161(targetIsDead));
            }

            return verdict == CallNewAccountVerdict.TheTransferBringsTheTargetIntoExistence
                ? preStateGas + ChargeNewAccount(program)
                : preStateGas;
        }

        private long PreStateValidationGas(Program program, byte[] toBytes, EvmUInt256 value) =>
            _rule.PreStateValidationGas(
                _accessRule.GetAccessCost(program, toBytes),
                CallMemoryHelper.Calculate(
                    program,
                    program.StackPeekAtU256(3), program.StackPeekAtU256(4),
                    program.StackPeekAtU256(5), program.StackPeekAtU256(6)),
                carriesValue: !value.IsZero);


        private long ChargeNewAccount(Program program)
        {
            if (StateGasMeter.ChargeNewAccountStateGas(program, _rule.NewAccountStateGas))
            {
                program.CallNewAccountStateGasCharged = true;
            }

            return _rule.NewAccountExecutionGas;
        }
    }
}
