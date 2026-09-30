namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// CALL's new-account charge from Spurious Dragon onward. EIP-161: where
    /// "<c>CALL</c> and <c>SUICIDE</c> would charge 25,000 gas when the
    /// destination is non-existent, now the charge SHALL <b>only</b> be levied
    /// if the operation transfers <i>more than zero value</i> and the
    /// destination account is <i>dead</i>".
    ///
    /// <para>Later forks rebind its parameters rather than restating it.
    /// EIP-8038: "For <c>CALL</c> and <c>CALLCODE</c> operations,
    /// <c>CALL_VALUE</c> (the Yellow Paper's <c>G_callvalue</c>) is redefined
    /// as <c>ACCOUNT_WRITE + CALL_STIPEND</c>, where <c>CALL_STIPEND</c>
    /// (<c>G_callstipend</c>, 2,300) is unchanged. […] The state-creation
    /// component of a value-transferring call to a dead account (as defined in
    /// EIP-161) is <c>GAS_NEW_ACCOUNT</c>, which this EIP does not reprice; it
    /// is repriced and metered in state-gas by EIP-8037." So at Amsterdam the
    /// flat execution charge is gone rather than repriced, and the creation is
    /// state gas alone - unlike SELFDESTRUCT, which keeps both.</para>
    /// </summary>
    public sealed class Eip161CallNewAccountRule : ICallNewAccountRule
    {
        private readonly long _valueTransferCost;
        private readonly long _newAccountExecutionGas;
        private readonly long _newAccountStateGas;

        private Eip161CallNewAccountRule(
            long valueTransferCost, long newAccountExecutionGas, long newAccountStateGas)
        {
            _valueTransferCost = valueTransferCost;
            _newAccountExecutionGas = newAccountExecutionGas;
            _newAccountStateGas = newAccountStateGas;
        }

        public static Eip161CallNewAccountRule AsIntroducedByEip161() =>
            new Eip161CallNewAccountRule(
                GasConstants.CALL_VALUE_TRANSFER,
                GasConstants.CALL_NEW_ACCOUNT,
                newAccountStateGas: 0);

        public static Eip161CallNewAccountRule AsReboundByEip8038() =>
            new Eip161CallNewAccountRule(
                GasConstants.EIP8038_CALL_VALUE_TRANSFER,
                newAccountExecutionGas: 0,
                GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);

        public long PreStateValidationGas(long accessCost, long memoryExpansionCost, bool carriesValue) =>
            accessCost + memoryExpansionCost + (carriesValue ? _valueTransferCost : 0);

        public CallNewAccountVerdict JudgeTheTransfer(CallTransfer transfer)
        {
            if (!transfer.CarriesValue) return CallNewAccountVerdict.NoNewAccountIsCreated;
            if (!transfer.TheTargetDeadnessIsKnown) return CallNewAccountVerdict.NotUntilTheTargetDeadnessIsKnown;

            return transfer.TheTargetIsDeadPerEip161
                ? CallNewAccountVerdict.TheTransferBringsTheTargetIntoExistence
                : CallNewAccountVerdict.NoNewAccountIsCreated;
        }

        public long NewAccountExecutionGas => _newAccountExecutionGas;

        public long NewAccountStateGas => _newAccountStateGas;
    }
}
