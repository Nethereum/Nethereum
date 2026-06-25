namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-161 STATE_CLEARING policy. Decides whether touched-and-empty
    /// accounts are deleted at end of transaction (the EIP-161
    /// delete-empty-objects rule).
    ///
    /// <para><b>Fork history:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier through Tangerine Whistle — <see cref="Persist"/>.
    ///   Empty accounts persist in state forever once created. Combined
    ///   with the cheap-to-create cost at the time, this caused the
    ///   Shanghai DoS attacks that EIP-161 was designed to fix.</item>
    ///   <item>Spurious Dragon (EIP-161) onwards — <see cref="Eip161Clear"/>.
    ///   At end of every tx, any account that was touched during the tx
    ///   and is now empty (nonce=0, balance=0, no code) is deleted.</item>
    /// </list>
    ///
    /// <para><b>Coupling note:</b> when <see cref="Eip161Clear"/> is
    /// active, the executor also sets
    /// <c>ExecutionStateService.TouchPersistsOnRevert = true</c> so
    /// touched flags survive snapshot reverts (matches the canonical journal
    /// behaviour where the touch is recorded outside the change-set that is
    /// popped on revert).</para>
    /// </summary>
    public abstract class EmptyAccountPolicy
    {
        /// <summary>
        /// Pre-EIP-161: empty accounts persist. No cleanup at end of tx.
        /// </summary>
        public static readonly EmptyAccountPolicy Persist = new PersistPolicy();

        /// <summary>
        /// EIP-161 (Spurious Dragon+): empty accounts that were touched
        /// during the tx are deleted at end of tx.
        /// </summary>
        public static readonly EmptyAccountPolicy Eip161Clear = new Eip161ClearPolicy();

        /// <summary>
        /// True when the cleanup rule should run at end of tx AND the
        /// executor should enable TouchPersistsOnRevert so that touched
        /// flags survive snapshot reverts (required for EIP-161 semantics).
        /// </summary>
        public abstract bool DeletesEmpties { get; }

        private sealed class PersistPolicy : EmptyAccountPolicy
        {
            public override bool DeletesEmpties => false;
        }

        private sealed class Eip161ClearPolicy : EmptyAccountPolicy
        {
            public override bool DeletesEmpties => true;
        }
    }
}
