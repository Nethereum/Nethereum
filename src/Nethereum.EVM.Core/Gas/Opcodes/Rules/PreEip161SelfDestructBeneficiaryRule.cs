namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// SELFDESTRUCT's new-account charge at Tangerine Whistle, the one fork
    /// that charges it and has not yet adopted EIP-161. EIP-161 states this
    /// rule in the course of replacing it: "<c>CALL</c> and <c>SUICIDE</c>
    /// would charge 25,000 gas when the destination is non-existent". Checked
    /// against EELS
    /// <c>forks/tangerine_whistle/vm/instructions/system.py::selfdestruct</c>,
    /// whose condition is <c>not account_exists(state, beneficiary)</c>.
    ///
    /// <para>Non-existent, not empty, and not conditional on what the sweep
    /// carries: an account record that exists but holds nothing still counts as
    /// existing here, which is why this is a rule of its own rather than a
    /// binding of the EIP-161 one. It is also why it never asks for the
    /// contract's balance.</para>
    /// </summary>
    public sealed class PreEip161SelfDestructBeneficiaryRule : ISelfDestructBeneficiaryRule
    {
        public static readonly PreEip161SelfDestructBeneficiaryRule Instance =
            new PreEip161SelfDestructBeneficiaryRule();

        public bool TheBeneficiaryEntersTheAccessList => false;

        public long PreStateValidationGas(bool isColdAccess) => GasConstants.SELFDESTRUCT_COST;

        public SelfDestructSweepVerdict JudgeTheSweep(SelfDestructSweep sweep) =>
            sweep.TheBeneficiaryHasAnAccountRecord
                ? SelfDestructSweepVerdict.NoNewAccountIsCreated
                : SelfDestructSweepVerdict.TheSweepBringsTheBeneficiaryIntoExistence;

        public long NewAccountExecutionGas => GasConstants.CALL_NEW_ACCOUNT;

        public long NewAccountStateGas => 0;
    }
}
