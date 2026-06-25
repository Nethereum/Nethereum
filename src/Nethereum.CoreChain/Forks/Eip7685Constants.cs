namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// EIP-7685: General-purpose execution-layer requests, activated at
    /// Prague. The execution layer surfaces three request types up to the
    /// consensus layer per block:
    ///   0x00 deposit         — sourced from tx logs of the existing
    ///                          beacon deposit contract (EIP-6110)
    ///   0x01 withdrawal      — end-of-block system call to the
    ///                          withdrawal predeploy (EIP-7002)
    ///   0x02 consolidation   — end-of-block system call to the
    ///                          consolidation predeploy (EIP-7251)
    /// The block header's <c>requests_hash</c> commits to the
    /// concatenation per EIP-7685.
    ///
    /// <para>The two predeploys are invoked by <c>SYSTEM_ADDRESS</c>
    /// with empty calldata and a fixed 30M-gas limit, after every tx in
    /// the block but before the post-state root is computed. Each
    /// predeploy returns the serialised request list as call output and
    /// mutates its own ring-buffer storage to dequeue requests. Per EIP
    /// the system call MUST succeed — failure halts the chain.</para>
    /// </summary>
    public static class Eip7685Constants
    {
        /// <summary>
        /// Spec-mandated caller for all post-Prague end-of-block system
        /// calls. Has no balance / nonce / code; its only role is to
        /// supply a deterministic sender for the predeploy invocation
        /// so chain state cannot influence the outcome.
        /// </summary>
        public const string SystemAddress = "0xfffffffffffffffffffffffffffffffffffffffe";

        /// <summary>
        /// EIP-7002 withdrawal-request predeploy. Receives end-of-block
        /// system call with empty calldata, returns the serialised
        /// withdrawal request list, dequeues serviced entries from its
        /// own ring buffer.
        /// </summary>
        public const string WithdrawalRequestPredeployAddress = "0x00000961Ef480Eb55e80D19ad83579A64c007002";

        /// <summary>
        /// EIP-7251 consolidation-request predeploy. Symmetric to the
        /// withdrawal predeploy; serves consolidation requests instead.
        /// </summary>
        public const string ConsolidationRequestPredeployAddress = "0x0000BBdDc7CE488642fb579F8B00f3a590007251";

        /// <summary>
        /// Per-call gas budget for every Prague+ system call. Both
        /// EIP-7002 and EIP-7251 specify the same 30M limit so the
        /// predeploy never OOGs under canonical pre-state.
        /// </summary>
        public const long SystemCallGasLimit = 30_000_000;

        /// <summary>Deposit request type byte per EIP-6110.</summary>
        public const byte DepositRequestType = 0x00;

        /// <summary>Withdrawal request type byte per EIP-7002.</summary>
        public const byte WithdrawalRequestType = 0x01;

        /// <summary>Consolidation request type byte per EIP-7251.</summary>
        public const byte ConsolidationRequestType = 0x02;
    }
}
