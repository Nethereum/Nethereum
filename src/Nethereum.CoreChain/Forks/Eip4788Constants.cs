namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// EIP-4788 beacon block root in the EVM. Activated at Cancun. Each block
    /// stores the parent beacon block root into a system contract so the EVM
    /// can verify beacon-chain data without trusting an oracle. The
    /// <see cref="BlockExecutor"/> engine writes the root pre-tx on every
    /// Cancun+ block.
    /// </summary>
    public static class Eip4788Constants
    {
        /// <summary>
        /// System contract that holds the ring buffer of parent beacon block
        /// roots, indexed by (timestamp % HISTORY_BUFFER_LENGTH). Spec-mandated
        /// fixed address. Storage layout is two slots per timestamp: the
        /// timestamp itself at slot N, and the root at slot N + buffer length.
        /// </summary>
        public const string BeaconRootsAddress = "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";

        /// <summary>
        /// Ring-buffer length in slots. Wraps every ~27 hours at 12-second
        /// slot time. EIP-4788 §Specification.
        /// </summary>
        public const int HistoryBufferLength = 8191;
    }
}
