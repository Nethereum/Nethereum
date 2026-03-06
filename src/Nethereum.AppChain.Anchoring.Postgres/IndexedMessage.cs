using System;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class IndexedMessage
    {
        public long Id { get; set; }
        public long SourceChainId { get; set; }
        public long MessageId { get; set; }
        public long TargetChainId { get; set; }
        public string Sender { get; set; } = "";
        public string Target { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public long BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }
}
