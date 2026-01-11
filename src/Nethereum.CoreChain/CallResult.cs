using System.Numerics;

namespace Nethereum.CoreChain
{
    public class CallResult
    {
        public bool Success { get; set; }
        public byte[] ReturnData { get; set; }
        public string RevertReason { get; set; }
        public BigInteger GasUsed { get; set; }
    }
}
