using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// The SELFDESTRUCT entry in the opcode gas table. It reads the operand,
    /// prices what the EIP calls pre-state validation, checks the frame can
    /// afford it, reads the beneficiary and hands the sweep to the rule, which
    /// prices it without touching state.
    ///
    /// <para>EIP-7928: "Pre-state validation MUST pass before any state access
    /// occurs. If pre-state validation fails, the target resource (address or
    /// storage slot) is never accessed and MUST NOT be included in the BAL."
    /// The operand is read before the first state call, and the affordability
    /// check sits between the two phases, so nothing after a refusal reaches
    /// the beneficiary. The beneficiary is still marked warm ahead of that
    /// check: EIP-2929 makes the surcharge and the entry into
    /// <c>accessed_addresses</c> one step, and the charge being refused does
    /// not undo it.</para>
    ///
    /// <para><b>The three reads of the beneficiary are unconditional, and at
    /// Tangerine Whistle the rule discards all three.</b> They materialise the
    /// account and notify the block-access-list recorder, so dropping them
    /// where nothing consumes them would change the post-state trie at the
    /// forks that keep empty accounts and change
    /// <c>block_access_list_hash</c> where a list is built. They are performed
    /// here in the order the engine has always performed them, and whether
    /// they should happen at all is an open question with a row against it,
    /// not one this class settles.</para>
    ///
    /// <para>The contract's own balance is the read that is <i>not</i>
    /// unconditional. EIP-161 and EELS alike stop before it for a beneficiary
    /// that is already alive, so the rule is asked first from the beneficiary
    /// alone and asked again only if it says it cannot answer yet.</para>
    ///
    /// <para>One body serves both engines. Two things differ and both are
    /// split where they occur: the state reads, which the async host awaits
    /// against a reader that may be recording the witness as it goes while
    /// <c>EVM_SYNC</c> resolves from the pre-state it was given; and the halt
    /// line, which <c>EVM_SYNC</c> needs because an unaffordable charge sets a
    /// flag there rather than throwing.</para>
    /// </summary>
    public sealed class SelfDestructBeneficiaryResolvingGasCost : IOpcodeGasCostAsync
    {
        private readonly ISelfDestructBeneficiaryRule _rule;

        public SelfDestructBeneficiaryResolvingGasCost(ISelfDestructBeneficiaryRule rule)
        {
            _rule = rule;
        }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var beneficiaryBytes = program.StackPeekAt(0);
            var isColdAccess = MarkWarmAndReportColdAccess(program, beneficiaryBytes);
            var preStateGas = _rule.PreStateValidationGas(isColdAccess);

            program.RequireGas(preStateGas);
#if EVM_SYNC
            if (program.HasExecutionError) return 0;
#endif

            var state = program.ProgramContext.ExecutionStateService;
            var beneficiaryAddress = EvmAddress.From(beneficiaryBytes);
#if EVM_SYNC
            var balance = state.GetTotalBalance(beneficiaryAddress);
            var code = state.GetCode(beneficiaryAddress);
            var nonce = state.GetNonce(beneficiaryAddress);
#else
            var balance = await state.GetTotalBalanceAsync(beneficiaryAddress);
            var code = await state.GetCodeAsync(beneficiaryAddress);
            var nonce = await state.GetNonceAsync(beneficiaryAddress);
#endif
            var sweep = SweepTowards(state, beneficiaryAddress, balance, code, nonce);

            var verdict = _rule.JudgeTheSweep(sweep);
            if (verdict == SelfDestructSweepVerdict.NotUntilTheContractBalanceIsKnown)
            {
                var contract = program.ProgramContext.AddressContract;
#if EVM_SYNC
                var contractBalance = state.GetTotalBalance(contract);
#else
                var contractBalance = await state.GetTotalBalanceAsync(contract);
#endif
                verdict = _rule.JudgeTheSweep(sweep.AndTheContractBalance(contractBalance));
            }

            return verdict == SelfDestructSweepVerdict.TheSweepBringsTheBeneficiaryIntoExistence
                ? preStateGas + ChargeNewAccount(program)
                : preStateGas;
        }

        /// <summary>
        /// EIP-2929: "If the ETH recipient of a <c>SELFDESTRUCT</c> is not in
        /// <c>accessed_addresses</c> (regardless of whether or not the amount
        /// sent is nonzero), charge an additional <c>COLD_ACCOUNT_ACCESS_COST</c>
        /// on top of the existing gas costs, and add the ETH recipient to the
        /// set." The add is this method; the charge belongs to the rule.
        /// </summary>
        private bool MarkWarmAndReportColdAccess(Program program, byte[] beneficiaryBytes)
        {
            if (!_rule.TheBeneficiaryEntersTheAccessList) return false;
            if (program.IsAddressWarm(beneficiaryBytes)) return false;

            program.MarkAddressAsWarm(beneficiaryBytes);
            return true;
        }

        private static SelfDestructSweep SweepTowards(
            ExecutionStateService state, EvmAddress beneficiary, EvmUInt256 balance, byte[] code, EvmUInt256 nonce) =>
            SelfDestructSweep.OfTheBeneficiaryAlone(
                AccountExistenceFacts.Resolved(balance, code, nonce, HasAnAccountRecord(state, beneficiary)));

        private static bool HasAnAccountRecord(ExecutionStateService state, EvmAddress beneficiary) =>
            state.AccountsState.TryGetValue(beneficiary, out var account)
                && (account.WasInPreState || account.IsNewContract);

        private long ChargeNewAccount(Program program)
        {
            StateGasMeter.ChargeNewAccountStateGas(program, _rule.NewAccountStateGas);
            return _rule.NewAccountExecutionGas;
        }
    }
}
