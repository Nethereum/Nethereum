using System;
using System.Numerics;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageProcessingResult
    {
        public ulong SourceChainId { get; set; }
        public ulong MessageId { get; set; }
        public string Target { get; set; } = "";
        public bool Success { get; set; }
        public byte[] AppChainTxHash { get; set; } = Array.Empty<byte>();
        public byte[] ReturnDataHash { get; set; } = Array.Empty<byte>();
        public BigInteger GasUsed { get; set; }
    }
}
