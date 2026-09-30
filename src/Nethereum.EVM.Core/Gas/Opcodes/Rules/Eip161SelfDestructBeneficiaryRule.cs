namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// SELFDESTRUCT's new-account charge from Spurious Dragon onward.
    /// EIP-161: the charge "SHALL only be levied if the operation transfers
    /// more than zero value and the destination account is <i>dead</i>".
    ///
    /// <para>Later forks rebind its parameters rather than restating it.
    /// EIP-2929: "If the ETH recipient of a <c>SELFDESTRUCT</c> is not in
    /// <c>accessed_addresses</c> (regardless of whether or not the amount sent
    /// is nonzero), charge an additional <c>COLD_ACCOUNT_ACCESS_COST</c>" —
    /// and, unlike every other account-reading opcode, a warm recipient adds
    /// nothing. EIP-8038 reprices that surcharge and splits the flat charge:
    /// "For <c>SELFDESTRUCT</c>, an additional charge of <c>ACCOUNT_WRITE</c>
    /// is added if a positive balance is sent to a dead account (as defined in
    /// EIP-161) […] The state-creation component in this same case is
    /// <c>GAS_NEW_ACCOUNT</c>, which remains in place alongside the new
    /// <c>ACCOUNT_WRITE</c> charge and is metered in state-gas by
    /// EIP-8037."</para>
    /// </summary>
    public sealed class Eip161SelfDestructBeneficiaryRule : ISelfDestructBeneficiaryRule
    {
        private readonly bool _beneficiaryEntersTheAccessList;
        private readonly long _coldAccessSurcharge;
        private readonly long _newAccountExecutionGas;
        private readonly long _newAccountStateGas;

        private Eip161SelfDestructBeneficiaryRule(
            bool beneficiaryEntersTheAccessList,
            long coldAccessSurcharge,
            long newAccountExecutionGas,
            long newAccountStateGas)
        {
            _beneficiaryEntersTheAccessList = beneficiaryEntersTheAccessList;
            _coldAccessSurcharge = coldAccessSurcharge;
            _newAccountExecutionGas = newAccountExecutionGas;
            _newAccountStateGas = newAccountStateGas;
        }

        public static Eip161SelfDestructBeneficiaryRule AsIntroducedByEip161() =>
            new Eip161SelfDestructBeneficiaryRule(
                beneficiaryEntersTheAccessList: false,
                coldAccessSurcharge: 0,
                GasConstants.CALL_NEW_ACCOUNT,
                newAccountStateGas: 0);

        public static Eip161SelfDestructBeneficiaryRule WithTheEip2929AccessList() =>
            new Eip161SelfDestructBeneficiaryRule(
                beneficiaryEntersTheAccessList: true,
                GasConstants.COLD_ACCOUNT_ACCESS_COST,
                GasConstants.CALL_NEW_ACCOUNT,
                newAccountStateGas: 0);

        public static Eip161SelfDestructBeneficiaryRule AsReboundByEip8038() =>
            new Eip161SelfDestructBeneficiaryRule(
                beneficiaryEntersTheAccessList: true,
                GasConstants.EIP8038_COLD_ACCOUNT_ACCESS,
                GasConstants.EIP8038_ACCOUNT_WRITE,
                GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);

        public bool TheBeneficiaryEntersTheAccessList => _beneficiaryEntersTheAccessList;

        public long PreStateValidationGas(bool isColdAccess) =>
            GasConstants.SELFDESTRUCT_COST + (isColdAccess ? _coldAccessSurcharge : 0);

        public SelfDestructSweepVerdict JudgeTheSweep(SelfDestructSweep sweep)
        {
            if (!sweep.TheBeneficiaryIsEmptyPerEip161) return SelfDestructSweepVerdict.NoNewAccountIsCreated;
            if (!sweep.TheContractBalanceIsKnown) return SelfDestructSweepVerdict.NotUntilTheContractBalanceIsKnown;

            return sweep.TheSweepCarriesValue
                ? SelfDestructSweepVerdict.TheSweepBringsTheBeneficiaryIntoExistence
                : SelfDestructSweepVerdict.NoNewAccountIsCreated;
        }

        public long NewAccountExecutionGas => _newAccountExecutionGas;

        public long NewAccountStateGas => _newAccountStateGas;
    }
}
