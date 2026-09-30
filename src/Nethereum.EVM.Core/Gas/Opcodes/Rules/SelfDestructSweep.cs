using System;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// One SELFDESTRUCT sweep, as the rule pricing it sees it: the beneficiary
    /// the balance is going to, and — once anyone has needed to read it — the
    /// balance the contract is sending.
    ///
    /// <para>The contract's balance is deliberately absent until asked for.
    /// EIP-161 phrases the charge as a conjunction, "only be levied if the
    /// operation transfers more than zero value and the destination account is
    /// dead", and EELS reads the originator's balance only for a beneficiary
    /// that is not alive — <c>forks/spurious_dragon/vm/instructions/system.py</c>
    /// through <c>forks/amsterdam/…</c>. Reading it is not free: it materialises
    /// the account and notifies the block-access-list recorder. So the sweep
    /// starts without it, a rule that needs it says so, and a rule that asks
    /// for it before it was read is refused rather than told zero.</para>
    /// </summary>
    public readonly struct SelfDestructSweep
    {
        private readonly AccountExistenceFacts _beneficiary;
        private readonly EvmUInt256 _contractBalance;
        private readonly bool _contractBalanceIsKnown;
        private readonly bool _isResolved;

        private SelfDestructSweep(
            AccountExistenceFacts beneficiary, EvmUInt256 contractBalance, bool contractBalanceIsKnown)
        {
            _beneficiary = beneficiary;
            _contractBalance = contractBalance;
            _contractBalanceIsKnown = contractBalanceIsKnown;
            _isResolved = true;
        }

        public static SelfDestructSweep OfTheBeneficiaryAlone(AccountExistenceFacts beneficiary) =>
            new SelfDestructSweep(beneficiary, default(EvmUInt256), contractBalanceIsKnown: false);

        public SelfDestructSweep AndTheContractBalance(EvmUInt256 contractBalance)
        {
            RequireResolved();
            return new SelfDestructSweep(_beneficiary, contractBalance, contractBalanceIsKnown: true);
        }

        /// <summary>EIP-161: the beneficiary "is considered empty when it has
        /// no code and zero nonce and zero balance".</summary>
        public bool TheBeneficiaryIsEmptyPerEip161
        {
            get { RequireResolved(); return _beneficiary.IsEmptyPerEip161; }
        }

        /// <summary>EIP-161 on the rule it replaces: "<c>CALL</c> and
        /// <c>SUICIDE</c> would charge 25,000 gas when the destination is
        /// non-existent".</summary>
        public bool TheBeneficiaryHasAnAccountRecord
        {
            get { RequireResolved(); return _beneficiary.HasAnAccountRecord; }
        }

        /// <summary>EIP-161: the charge is levied only where "the operation
        /// transfers more than zero value".</summary>
        public bool TheSweepCarriesValue
        {
            get { RequireTheContractBalance(); return _contractBalance > 0; }
        }

        public bool TheContractBalanceIsKnown
        {
            get { RequireResolved(); return _contractBalanceIsKnown; }
        }

        private void RequireResolved()
        {
            if (!_isResolved)
                throw new InvalidOperationException(
                    "A SELFDESTRUCT sweep is judged only after its beneficiary has been read.");
        }

        private void RequireTheContractBalance()
        {
            RequireResolved();
            if (!_contractBalanceIsKnown)
                throw new InvalidOperationException(
                    "The contract's balance is read only for a sweep whose rule asked for it.");
        }
    }
}
