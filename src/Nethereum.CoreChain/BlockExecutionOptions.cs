namespace Nethereum.CoreChain
{
    /// <summary>
    /// Caller-supplied switches for one block execution. Used by the
    /// canonical <see cref="BlockExecutor"/> engine and the three wrappers
    /// (<see cref="BlockImporter"/>, <c>BlockProducer</c>, on-demand witness
    /// capture) to describe per-call behaviour without forking the engine
    /// signature.
    /// </summary>
    public sealed class BlockExecutionOptions
    {
        /// <summary>
        /// When <c>true</c>, no disk write is allowed. State mutations made
        /// during system calls, tx execution, and rewards stay in the
        /// in-memory <c>ExecutionStateService</c> and never reach
        /// <see cref="Storage.IStateStore"/> or
        /// <see cref="Storage.ITrieNodeStore"/>. The post-state root is also
        /// skipped (read-only execution does not need a finalised root —
        /// witness capture only cares about which accounts/slots were
        /// touched). Used by the on-demand
        /// <c>ChainNodeBase.CaptureBlockWitnessAsync</c> path.
        /// </summary>
        public bool ReadOnly { get; init; }

        /// <summary>
        /// When <c>true</c>, wrap the state reader in a
        /// <see cref="EVM.Witness.WitnessRecordingStateReader"/> so the
        /// engine can serialise a <see cref="EVM.Witness.BinaryBlockWitness"/>
        /// after execution and surface it on
        /// <see cref="BlockExecutionResult.WitnessBytes"/>.
        /// </summary>
        public bool CaptureWitness { get; init; }

        /// <summary>
        /// Raw 32-byte beacon-block root from the consensus layer. Required
        /// for the EIP-4788 system call at Cancun+; ignored on earlier forks
        /// and on the genesis block. Followers source this from
        /// <see cref="Model.BlockHeader.ParentBeaconBlockRoot"/>; the
        /// sequencer pulls it from its consensus client.
        /// </summary>
        public byte[]? ParentBeaconBlockRoot { get; init; }

        /// <summary>
        /// When set, opcode-level tracing is enabled for the transaction at
        /// the given index in the tx list. The captured trace lands on
        /// <c>BlockExecutionResult.Receipts[TraceTxIndex.Value].Traces</c> as
        /// <see cref="EVM.ProgramTrace"/> steps from the same production
        /// EVM path that runs sync, sequencer, and BlockReplay. Single source
        /// of truth — no duplicate tracing harness elsewhere.
        ///
        /// <para>Null (default) → no tracing on any tx, fastest path. Setting
        /// it adds memory + storage capture for one tx only; the rest of the
        /// block runs untraced.</para>
        /// </summary>
        public int? TraceTxIndex { get; init; }
    }
}
