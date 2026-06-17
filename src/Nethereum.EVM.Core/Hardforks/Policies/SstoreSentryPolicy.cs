namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-2200 reentrancy sentry policy for the SSTORE opcode. Decides
    /// whether SSTORE OOGs when gas remaining at the start of the opcode
    /// is at or below the 2300-gas threshold (one CALL_STIPEND).
    ///
    /// <para><b>Fork history:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier through Constantinople and Petersburg —
    ///   <see cref="Disabled"/>. EIP-1283 introduced net-gas SSTORE at
    ///   Constantinople but DID NOT add the sentry; SSTORE can run on as
    ///   little as 200 gas via the EIP-1283 NOOP path.</item>
    ///   <item>Istanbul onwards — <see cref="Eip2200Active"/>. EIP-2200
    ///   added the sentry to prevent the cheap NOOP path being abused by
    ///   reentrancy attacks (the Constantinople-1283 reentrancy concern
    ///   that caused the ChainSecurity audit).</item>
    /// </list>
    ///
    /// <para><b>Geth ref:</b> <c>core/vm/gas_table.go gasSStoreEIP2200</c>
    /// (line ~183 in 1.14.x): <c>if contract.Gas &lt;= params.SstoreSentryGasEIP2200 { return errors.New("not enough gas for reentrancy sentry") }</c>.</para>
    /// </summary>
    public abstract class SstoreSentryPolicy
    {
        /// <summary>
        /// Pre-EIP-2200 policy: SSTORE never OOGs from a gas-floor check.
        /// The opcode's own cost calculation still applies normally.
        /// </summary>
        public static readonly SstoreSentryPolicy Disabled = new DisabledPolicy();

        /// <summary>
        /// EIP-2200 (Istanbul+) policy: SSTORE OOGs when gas remaining
        /// at opcode entry is at or below the CALL_STIPEND threshold (2300).
        /// </summary>
        public static readonly SstoreSentryPolicy Eip2200Active = new Eip2200ActivePolicy();

        /// <summary>
        /// True when the executor must trigger OutOfGas for SSTORE at the
        /// given remaining-gas value. Checked before the SSTORE cost is
        /// computed or deducted.
        /// </summary>
        public abstract bool ShouldOog(long gasRemaining);

        private sealed class DisabledPolicy : SstoreSentryPolicy
        {
            public override bool ShouldOog(long gasRemaining) => false;
        }

        private sealed class Eip2200ActivePolicy : SstoreSentryPolicy
        {
            public override bool ShouldOog(long gasRemaining) => gasRemaining <= Gas.GasConstants.CALL_STIPEND;
        }
    }
}
