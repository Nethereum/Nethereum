using System;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// One CALL, as the rule pricing it sees it: the value it carries and -
    /// once a rule has needed it - whether its target is dead.
    ///
    /// <para>The target is deliberately absent until asked for. EIP-161
    /// phrases the charge as a conjunction, levied "only" where "the operation
    /// transfers <i>more than zero value</i> and the destination account is
    /// <i>dead</i>", and EELS reads the target only for a call that carries
    /// value - <c>forks/spurious_dragon/vm/instructions/system.py</c> through
    /// <c>forks/amsterdam/...</c>, whose <c>value == 0 or ...</c> and
    /// <c>has_value and ...</c> are the short-circuit. Reading it costs three
    /// state reads on the commonest instruction on the chain. So the transfer
    /// starts without it, a rule that needs it says so, and a rule that asks
    /// for it before it was read is refused rather than told false.</para>
    ///
    /// <para>Every predicate refuses an unresolved transfer, the value one
    /// included. A struct always has a <c>default</c>, and a default one
    /// carries a zero value - which reads as "no value transferred" and prices
    /// a value-bearing call into a dead account as owing nothing.</para>
    /// </summary>
    public readonly struct CallTransfer
    {
        private readonly EvmUInt256 _value;
        private readonly bool _targetIsDeadPerEip161;
        private readonly bool _targetDeadnessIsKnown;
        private readonly bool _isResolved;

        private CallTransfer(EvmUInt256 value, bool targetIsDeadPerEip161, bool targetDeadnessIsKnown)
        {
            _value = value;
            _targetIsDeadPerEip161 = targetIsDeadPerEip161;
            _targetDeadnessIsKnown = targetDeadnessIsKnown;
            _isResolved = true;
        }

        public static CallTransfer OfTheValueAlone(EvmUInt256 value) =>
            new CallTransfer(value, targetIsDeadPerEip161: false, targetDeadnessIsKnown: false);

        public CallTransfer AndTheTargetDeadnessPerEip161(bool targetIsDead)
        {
            RequireResolved();
            return new CallTransfer(_value, targetIsDead, targetDeadnessIsKnown: true);
        }

        /// <summary>EIP-161: the charge is levied only where "the operation
        /// transfers <i>more than zero value</i>".</summary>
        public bool CarriesValue
        {
            get { RequireResolved(); return !_value.IsZero; }
        }

        public bool TheTargetDeadnessIsKnown
        {
            get { RequireResolved(); return _targetDeadnessIsKnown; }
        }

        /// <summary>EIP-161: "An account is considered <i>dead</i> when either
        /// it is non-existent or it is <i>empty</i>", and "An account is
        /// considered <i>empty</i> when it has no code and zero nonce and zero
        /// balance".</summary>
        public bool TheTargetIsDeadPerEip161
        {
            get { RequireTheTarget(); return _targetIsDeadPerEip161; }
        }

        private void RequireResolved()
        {
            if (!_isResolved)
                throw new InvalidOperationException(
                    "A CALL is judged only after the value it carries has been read.");
        }

        private void RequireTheTarget()
        {
            RequireResolved();
            if (!_targetDeadnessIsKnown)
                throw new InvalidOperationException(
                    "The target is read only for a CALL whose rule asked for it.");
        }
    }
}
