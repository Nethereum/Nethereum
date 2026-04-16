namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// Cross-cutting rule <b>R2</b> — EIP-2929 cold/warm address access
    /// accounting. Used by every opcode that reads account state by
    /// address (<c>BALANCE</c>, <c>EXTCODESIZE</c>, <c>EXTCODECOPY</c>,
    /// <c>EXTCODEHASH</c>, the <c>CALL</c> family, <c>SELFDESTRUCT</c>).
    ///
    /// <para>
    /// The rule owns two responsibilities the caller must treat as a
    /// single atomic operation:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     Return the access cost — <c>COLD_ACCOUNT_ACCESS_COST</c>
    ///     (2600) if the address has not been touched in this
    ///     transaction, <c>WARM_STORAGE_READ_COST</c> (100) otherwise.
    ///   </description></item>
    ///   <item><description>
    ///     Side-effect: mark the address warm on first access so
    ///     subsequent rule invocations see it as warm.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// Sync-only — the warm tracker lives in
    /// <c>ExecutionStateService</c> and does not hit async storage.
    /// </para>
    /// </summary>
    public interface IAccessAccountRule
    {
        /// <summary>
        /// Returns the EIP-2929 access cost for <paramref name="addressBytes"/>
        /// and marks it warm as a side-effect if it wasn't already.
        /// </summary>
        long GetAccessCost(Program program, byte[] addressBytes);
    }
}
