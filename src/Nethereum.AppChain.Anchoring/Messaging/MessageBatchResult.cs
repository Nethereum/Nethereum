using System.Collections.Generic;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageBatchResult
    {
        public List<MessageProcessingResult> Results { get; set; } = new();
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
    }
}
