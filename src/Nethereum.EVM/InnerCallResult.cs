using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.EVM
{
    public class InnerCallResult
    {
        public CallInput CallInput { get; set; }
        public int FrameType { get; set; }
        public int Depth { get; set; }
        public BigInteger GasUsed { get; set; }
        public byte[] Output { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public string RevertReason { get; set; }
    }
}
