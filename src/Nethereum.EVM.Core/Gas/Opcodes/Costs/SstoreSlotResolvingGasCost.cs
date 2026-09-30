using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    /// <summary>
    /// The SSTORE entry in the opcode gas table. It asks the rule whether the
    /// frame may read at all, reads the operands, resolves the slot the opcode
    /// is about to write and hands it to the rule, which prices it without
    /// touching state.
    ///
    /// <para>EIP-7928: "Pre-state validation MUST pass before any state access
    /// occurs. If pre-state validation fails, the target resource (address or
    /// storage slot) is never accessed and MUST NOT be included in the BAL."
    /// The refusal is asked first and both operands are read before the first
    /// state call, so nothing this class does can warm or record a slot the
    /// opcode never reached. The unaffordable charge a refusal returns is the
    /// table's own marker and carries no fork price — every price in the SSTORE
    /// family belongs to the rule.</para>
    ///
    /// <para>That is only half the guarantee, and the missing half is not ours
    /// to give here. <c>Program.StackPeekAt</c> throws on underflow in the async
    /// build but sets an error flag and returns a zero word under
    /// <c>EVM_SYNC</c>, so the guest runs on and DOES warm and record the slot
    /// where the host records nothing. The two engines therefore disagree about
    /// the block access list for an SSTORE with too few operands. Pre-existing,
    /// tracked separately — see the stack-underflow divergence row.</para>
    ///
    /// <para>One body serves both engines. The slot read is the one thing they
    /// do differently — the async host awaits a state reader that may be
    /// recording the witness as it goes, while <c>EVM_SYNC</c> reads the
    /// pre-state it was already given — so that call alone is split and
    /// everything around it is written once.</para>
    /// </summary>
    public sealed class SstoreSlotResolvingGasCost : IOpcodeGasCostAsync
    {
        private readonly ISstoreGasRule _rule;

        public SstoreSlotResolvingGasCost(ISstoreGasRule rule)
        {
            _rule = rule;
        }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            if (_rule.RefusesTheFrameBeforeReadingTheSlot(program.GasRemaining))
                return GasConstants.OVERFLOW_GAS_COST;

            var slotKey = program.StackPeekAtU256(0);
            var newValue = program.StackPeekAt(1).PadTo32Bytes();
            var state = program.ProgramContext.ExecutionStateService;
            var contract = program.ProgramContext.AddressContract;
            var account = state.CreateOrGetAccountExecutionState(contract);
            var isColdAccess = MarkWarmAndReportColdAccess(account, slotKey);
#if EVM_SYNC
            var currentValue = state.GetFromStorage(contract, slotKey);
#else
            var currentValue = await state.GetFromStorageAsync(contract, slotKey);
#endif
            return _rule.GetGasCost(ResolveSlot(account, slotKey, currentValue, newValue, isColdAccess));
        }

        /// <summary>
        /// EIP-2929: "check if the <c>(address, storage_key)</c> pair is in
        /// <c>accessed_storage_keys</c>. If it is not, charge an additional
        /// <c>COLD_SLOAD_COST</c> gas, and add the pair to
        /// <c>accessed_storage_keys</c>." The check and the add are one step;
        /// the charge belongs to the rule.
        /// </summary>
        private static bool MarkWarmAndReportColdAccess(AccountExecutionState account, EvmUInt256 slotKey)
        {
            var isColdAccess = !account.IsStorageKeyWarm(slotKey);
            account.MarkStorageKeyAsWarm(slotKey);
            return isColdAccess;
        }

        private static SstoreSlotState ResolveSlot(
            AccountExecutionState account, EvmUInt256 slotKey, byte[] currentValue, byte[] newValue, bool isColdAccess)
        {
            var current = StoredWord(currentValue);
            var originalValues = account.OriginalStorageValues;
            var original = originalValues.ContainsKey(slotKey) ? StoredWord(originalValues[slotKey]) : current;

            return SstoreSlotState.Resolved(original, current, newValue, isColdAccess);
        }

        private static byte[] StoredWord(byte[] value) =>
            value?.PadTo32Bytes() ?? ByteUtil.InitialiseEmptyByteArray(32);
    }
}
