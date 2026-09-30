using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-2780 §Intrinsic gas costs: "if self-transfer (<c>tx.sender == tx.to</c>), there
    /// are no charges; if <c>None</c> (contract-creation transaction), charge
    /// <c>CREATE_ACCESS</c> in execution gas; the new-account state charge depends on whether
    /// the deployment target exists and is charged at runtime; otherwise, charge
    /// <c>COLD_ACCOUNT_ACCESS</c> in execution gas (covering the recipient touch, charged at
    /// the cold rate unconditionally)".
    ///
    /// <para>And on <c>tx.value</c>: "if non-zero and contract creation, there are no charges;
    /// the recipient balance write is already covered by <c>CREATE_ACCESS</c>; otherwise, charge
    /// <c>TX_VALUE_COST</c> in execution gas" — which is why the two early returns precede the
    /// value branch rather than falling through it.</para>
    /// </summary>
    public sealed class Eip2780RecipientGasRule : IRecipientGasRule
    {
        private const long CreateAccess = GasConstants.EIP8038_CREATE_ACCESS;
        private const long ColdAccountAccess = GasConstants.EIP8038_COLD_ACCOUNT_ACCESS;
        private const long TxValueCost = GasConstants.EIP2780_TX_VALUE_COST;

        public static readonly Eip2780RecipientGasRule Instance = new Eip2780RecipientGasRule();

        public long CalculateRecipientGas(bool isContractCreation, bool isSelfTransfer, bool hasValue)
        {
            if (isContractCreation)
                return CreateAccess;

            if (isSelfTransfer)
                return 0;

            long gas = ColdAccountAccess;
            if (hasValue)
                gas += TxValueCost;
            return gas;
        }
    }
}
