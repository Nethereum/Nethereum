using System;
using System.Numerics;

namespace Nethereum.AppChain.Anchoring
{
    public class AnchorInfo
    {
        public BigInteger BlockNumber { get; set; }
        public byte[] StateRoot { get; set; } = Array.Empty<byte>();
        public byte[] TransactionsRoot { get; set; } = Array.Empty<byte>();
        public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
        public long Timestamp { get; set; }
        public byte[]? AnchorTxHash { get; set; }
        public BigInteger? AnchorBlockNumber { get; set; }
        public AnchorStatus Status { get; set; } = AnchorStatus.Pending;
        public string? ErrorMessage { get; set; }
    }

    public enum AnchorStatus
    {
        Pending,
        Submitted,
        Confirmed,
        Failed
    }
}
