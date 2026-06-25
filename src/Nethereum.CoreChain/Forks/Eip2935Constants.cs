namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// EIP-2935 historical block hashes from state. Activated at Prague. Each
    /// block stores the parent block hash into a system contract so the EVM's
    /// BLOCKHASH opcode can resolve via state read instead of consulting a
    /// chain-history oracle. The <see cref="BlockExecutor"/> engine writes
    /// the parent hash pre-tx on every Prague+ block.
    /// </summary>
    public static class Eip2935Constants
    {
        /// <summary>
        /// System contract that holds the ring buffer of parent block hashes,
        /// indexed by (blockNumber % HistoryServeWindow). Spec-mandated fixed
        /// address: <c>0x0000F90827F1C53a10cb7A02335B175320002935</c>.
        /// </summary>
        public const string HistoryStorageAddress = "0x0000F90827F1C53a10cb7A02335B175320002935";

        /// <summary>
        /// Ring-buffer length in slots — 8191 blocks back. EIP-2935 §Specification.
        /// </summary>
        public const int HistoryServeWindow = 8191;
    }
}
