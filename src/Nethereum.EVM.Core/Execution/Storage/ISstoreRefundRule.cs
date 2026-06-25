namespace Nethereum.EVM.Execution.Storage
{
    /// <summary>
    /// End-of-SSTORE refund accounting strategy. Per-fork registration on
    /// <see cref="HardforkConfig.SstoreRefundRule"/>.
    /// - <see cref="LegacySstoreRefundRule"/>: Frontier-Byzantium and
    ///   Petersburg (EIP-1283 reverted). Single rule: any non-zero to zero
    ///   transition refunds <c>SstoreClearsSchedule</c>; nothing else.
    /// - <see cref="Eip1283SstoreRefundRule"/>: Constantinople onward
    ///   (Istanbul EIP-2200, London EIP-3529, etc. — same net-gas shape with
    ///   different constants from <c>SstoreClearsSchedule</c>, <c>SstoreSetRefund</c>,
    ///   <c>SstoreResetRefund</c> on the per-fork config).
    /// </summary>
    public interface ISstoreRefundRule
    {
        void Apply(Program program, byte[] currentVal, byte[] newVal, byte[] origVal);
    }
}
