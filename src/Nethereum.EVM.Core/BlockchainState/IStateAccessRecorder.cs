using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    /// <summary>
    /// Notified whenever execution reads account or storage state.
    ///
    /// <para>Exists because EIP-7928 needs to know what a transaction actually
    /// ACCESSED, and nothing else in the engine answers that question. The warm
    /// sets do not: EIP-3651 and the precompiles are pre-warmed without being
    /// touched. The account dictionary does not either, in the witness executor:
    /// every account in the witness is pre-loaded into every transaction, so
    /// membership means "present in the block's witness", not "read here". A
    /// slot read without being changed is then indistinguishable from one never
    /// looked at, and a read is exactly what the access list has to report.</para>
    ///
    /// <para>Called at the accessors themselves, which is where the reference
    /// records the same thing (<c>get_account</c>, <c>get_storage</c>), rather
    /// than inferred afterwards from a state dump.</para>
    /// </summary>
    public interface IStateAccessRecorder
    {
        void RecordAccountRead(string address);

        void RecordStorageRead(string address, EvmUInt256 key);
    }
}
