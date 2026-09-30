using System.Numerics;

namespace Nethereum.CoreChain
{
    public class CallResult
    {
        public bool Success { get; set; }
        public byte[] ReturnData { get; set; }
        public string RevertReason { get; set; }
        public BigInteger GasUsed { get; set; }

        /// <summary>
        /// EIP-8037 §Transaction-level accounting: <i>"only apply this gas limit to execution gas,
        /// not state gas"</i>. From Amsterdam the two are metered separately and a caller funding a
        /// transaction needs both, so a simulation reports both. State gas is zero
        /// before Amsterdam.
        /// </summary>
        public BigInteger ExecutionGasUsed { get; set; }

        public BigInteger StateGasUsed { get; set; }

        public BigInteger IntrinsicGasUsed { get; set; }
    }
}
