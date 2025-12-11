using System;

namespace Nethereum.Consensus.LightClient
{
    /// <summary>
    /// Represents the execution-layer payload extracted from the latest finalized beacon header.
    /// </summary>
    public class TrustedExecutionHeader
    {
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public ulong BlockNumber { get; set; }
        public byte[] StateRoot { get; set; } = Array.Empty<byte>();
        public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
        public DateTimeOffset Timestamp { get; set; }
    }
}
